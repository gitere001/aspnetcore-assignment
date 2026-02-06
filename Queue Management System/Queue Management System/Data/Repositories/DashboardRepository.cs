using Queue_Management_System.Data;
using Queue_Management_System.Models;
using Npgsql;

namespace Queue_Management_System.Data.Repositories
{
    public class DashboardRepository
    {
        private readonly DatabaseService _db;

        // Kenya Timezone: East Africa Time (EAT) = UTC+3
        private static readonly TimeSpan KenyaOffset = TimeSpan.FromHours(3);

        public DashboardRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task<DashboardStatsDto> GetDashboardStats(DateTime date)
        {
            var stats = new DashboardStatsDto();
            stats.Timestamp = DateTime.UtcNow;

            // Convert to Kenya time for accurate "today" calculation
            var kenyaNow = DateTime.UtcNow.Add(KenyaOffset);
            var kenyaToday = kenyaNow.Date;

            // Get date ranges in UTC (database stores UTC)
            // "Today" in Kenya = midnight Kenya time to now Kenya time, converted to UTC
            var todayStart = kenyaToday.Subtract(KenyaOffset); // Kenya midnight in UTC
            var todayEnd = kenyaNow.Subtract(KenyaOffset);     // Kenya now in UTC

            // Yesterday in Kenya time
            var yesterdayStart = kenyaToday.AddDays(-1).Subtract(KenyaOffset);
            var yesterdayEnd = kenyaToday.AddTicks(-1).Subtract(KenyaOffset);

            try
            {
                // Execute all queries in parallel for maximum performance
                var tasks = new Task[]
                {
                    // 1. Total Served Today
                    Task.Run(async () =>
                    {
                        var sql = @"
                            SELECT COUNT(*)
                            FROM tickets
                            WHERE status = 'Finished'
                            AND created_at >= @todayStart
                            AND created_at <= @todayEnd";

                        var count = await _db.ExecuteCountAsync(sql,
                            new NpgsqlParameter("@todayStart", todayStart),
                            new NpgsqlParameter("@todayEnd", todayEnd));
                        stats.TotalServed = (int)count;
                    }),

                    // 2. Total Served Yesterday
                    Task.Run(async () =>
                    {
                        var sql = @"
                            SELECT COUNT(*)
                            FROM tickets
                            WHERE status = 'Finished'
                            AND created_at >= @yesterdayStart
                            AND created_at <= @yesterdayEnd";

                        var count = await _db.ExecuteCountAsync(sql,
                            new NpgsqlParameter("@yesterdayStart", yesterdayStart),
                            new NpgsqlParameter("@yesterdayEnd", yesterdayEnd));

                        var yesterdayCount = (int)count;
                        stats.ServedVsYesterday = yesterdayCount > 0
                            ? Math.Round(((stats.TotalServed - yesterdayCount) / (double)yesterdayCount) * 100, 1)
                            : (stats.TotalServed > 0 ? 100 : 0);
                    }),

                    // 3. Average Wait Time Today
                    Task.Run(async () =>
                    {
                        var sql = @"
                            SELECT
                                COALESCE(
                                    AVG(
                                        CASE
                                            WHEN waiting_time_seconds > 0
                                            THEN waiting_time_seconds
                                            ELSE NULL
                                        END
                                    ),
                                    0
                                ) as avg_wait
                            FROM tickets
                            WHERE status = 'Finished'
                            AND created_at >= @todayStart
                            AND created_at <= @todayEnd";

                        var avgSeconds = await _db.ExecuteScalarAsync<double>(sql,
                            new NpgsqlParameter("@todayStart", todayStart),
                            new NpgsqlParameter("@todayEnd", todayEnd));

                        stats.AverageWaitTime = Math.Round(avgSeconds / 60.0, 1);
                    }),

                    // 4. Active Service Points
                    Task.Run(async () =>
                    {
                        var sql = @"
                            SELECT COUNT(*)
                            FROM service_points
                            WHERE is_active = true";

                        var count = await _db.ExecuteCountAsync(sql);
                        stats.ActiveServicePoints = (int)count;
                    }),

                    // 5. Customers Waiting Now
                    Task.Run(async () =>
                    {
                        var sql = @"
                            SELECT COUNT(*)
                            FROM tickets
                            WHERE status = 'Waiting'
                            AND created_at >= @todayStart
                            AND created_at <= @todayEnd";

                        var count = await _db.ExecuteCountAsync(sql,
                            new NpgsqlParameter("@todayStart", todayStart),
                            new NpgsqlParameter("@todayEnd", todayEnd)
                        );
                        stats.CustomersWaiting = (int)count;
                    }),

                    // 6. Service Efficiency
                    Task.Run(async () =>
                    {
                        var sql = @"
                            WITH called_tickets AS (
                                SELECT COUNT(*) as total_called
                                FROM tickets
                                WHERE status IN ('Called', 'Serving', 'Finished', 'NoShow')
                                AND created_at >= @todayStart
                                AND created_at <= @todayEnd
                            ),
                            finished_tickets AS (
                                SELECT COUNT(*) as total_finished
                                FROM tickets
                                WHERE status = 'Finished'
                                AND created_at >= @todayStart
                                AND created_at <= @todayEnd
                            )
                            SELECT
                                CASE
                                    WHEN ct.total_called > 0
                                    THEN ROUND((ft.total_finished::decimal / ct.total_called) * 100, 1)
                                    ELSE 0
                                END as efficiency
                            FROM called_tickets ct, finished_tickets ft";

                        stats.ServiceEfficiency = await _db.ExecuteScalarAsync<double>(sql,
                            new NpgsqlParameter("@todayStart", todayStart),
                            new NpgsqlParameter("@todayEnd", todayEnd));
                    }),

                    // 7. Tickets by Service (for chart)
                    Task.Run(async () =>
                    {
                        var sql = @"
                            SELECT
                                s.name as service_name,
                                COUNT(t.id) as ticket_count,
                                s.id as service_id
                            FROM services s
                            LEFT JOIN tickets t ON s.id = t.service_id
                                AND t.created_at >= @todayStart
                                AND t.created_at <= @todayEnd
                                AND t.status = 'Finished'
                            GROUP BY s.id, s.name
                            ORDER BY ticket_count DESC";

                        var ticketsByService = await _db.ExecuteReaderAsync(sql,
                            new NpgsqlParameter("@todayStart", todayStart),
                            new NpgsqlParameter("@todayEnd", todayEnd));

                        stats.TicketsByService = ticketsByService.Select(row => new ServiceTicketsDto
                        {
                            ServiceName = row["service_name"].ToString() ?? "Unknown",
                            TicketCount = Convert.ToInt32(row["ticket_count"]),
                            Percentage = 0, // Will calculate after
                            Color = GetColorForService(Convert.ToInt32(row["service_id"]))
                        }).ToList();
                    }),

                    // 8. Hourly Tickets - FIXED: Show served and noShow for TODAY's hours only (Kenya time)
                    Task.Run(async () =>
                    {
                        var sql = @"
                            WITH kenya_hours AS (
                                SELECT
                                    generate_series(
                                        date_trunc('hour', @todayStart + INTERVAL '3 hours'),
                                        date_trunc('hour', @todayEnd + INTERVAL '3 hours'),
                                        interval '1 hour'
                                    ) as hour_start
                            )
                            SELECT
                                TO_CHAR(kh.hour_start, 'HH24:MI') as hour,
                                COALESCE(COUNT(t.id) FILTER (WHERE t.status = 'Finished'), 0) as served,
                                COALESCE(COUNT(t.id) FILTER (WHERE t.status = 'NoShow'), 0) as noshow
                            FROM kenya_hours kh
                            LEFT JOIN tickets t ON
                                date_trunc('hour', t.finished_at + INTERVAL '3 hours') = kh.hour_start
                                AND t.finished_at >= @todayStart
                                AND t.finished_at <= @todayEnd
                                AND t.status IN ('Finished', 'NoShow')
                            GROUP BY kh.hour_start
                            ORDER BY kh.hour_start";

                        var hourlyData = await _db.ExecuteReaderAsync(sql,
                            new NpgsqlParameter("@todayStart", todayStart),
                            new NpgsqlParameter("@todayEnd", todayEnd));

                        stats.HourlyTickets = hourlyData.Select(row => new HourlyTicketsDto
                        {
                            Hour = row["hour"].ToString() ?? "00:00",
                            Served = Convert.ToInt32(row["served"]),
                            NoShow = Convert.ToInt32(row["noshow"])
                        }).ToList();
                    }),

                    // 9. Average Wait Time Yesterday (for comparison)
                    Task.Run(async () =>
                    {
                        var sql = @"
                            SELECT
                                COALESCE(
                                    AVG(
                                        CASE
                                            WHEN waiting_time_seconds > 0
                                            THEN waiting_time_seconds
                                            ELSE NULL
                                        END
                                    ),
                                    0
                                ) as avg_wait
                            FROM tickets
                            WHERE status = 'Finished'
                            AND created_at >= @yesterdayStart
                            AND created_at <= @yesterdayEnd";

                        var avgSeconds = await _db.ExecuteScalarAsync<double>(sql,
                            new NpgsqlParameter("@yesterdayStart", yesterdayStart),
                            new NpgsqlParameter("@yesterdayEnd", yesterdayEnd));

                        var yesterdayAvgMinutes = Math.Round(avgSeconds / 60.0, 1);
                        stats.WaitTimeVsYesterday = yesterdayAvgMinutes > 0
                            ? Math.Round(((stats.AverageWaitTime - yesterdayAvgMinutes) / yesterdayAvgMinutes) * 100, 1)
                            : (stats.AverageWaitTime > 0 ? 100 : 0);
                    }),

                    // 10. Service Efficiency Yesterday (for comparison)
                    Task.Run(async () =>
                    {
                        var sql = @"
                            WITH called_tickets AS (
                                SELECT COUNT(*) as total_called
                                FROM tickets
                                WHERE status IN ('Called', 'Serving', 'Finished', 'NoShow')
                                AND created_at >= @yesterdayStart
                                AND created_at <= @yesterdayEnd
                            ),
                            finished_tickets AS (
                                SELECT COUNT(*) as total_finished
                                FROM tickets
                                WHERE status = 'Finished'
                                AND created_at >= @yesterdayStart
                                AND created_at <= @yesterdayEnd
                            )
                            SELECT
                                CASE
                                    WHEN ct.total_called > 0
                                    THEN ROUND((ft.total_finished::decimal / ct.total_called) * 100, 1)
                                    ELSE 0
                                END as efficiency
                            FROM called_tickets ct, finished_tickets ft";

                        var yesterdayEfficiency = await _db.ExecuteScalarAsync<double>(sql,
                            new NpgsqlParameter("@yesterdayStart", yesterdayStart),
                            new NpgsqlParameter("@yesterdayEnd", yesterdayEnd));

                        stats.EfficiencyVsYesterday = yesterdayEfficiency > 0
                            ? Math.Round(((stats.ServiceEfficiency - yesterdayEfficiency) / yesterdayEfficiency) * 100, 1)
                            : (stats.ServiceEfficiency > 0 ? 100 : 0);
                    }),

                    // 11. Fastest Service Point
                    Task.Run(async () =>
                    {
                        var sql = @"
                            SELECT
                                sp.name,
                                AVG(t.service_time_seconds) as avg_service_time
                            FROM service_points sp
                            INNER JOIN tickets t ON sp.id = t.service_point_id
                            WHERE t.status = 'Finished'
                                AND t.service_time_seconds > 0
                                AND t.created_at >= @todayStart
                                AND t.created_at <= @todayEnd
                            GROUP BY sp.id, sp.name
                            ORDER BY avg_service_time ASC
                            LIMIT 1";

                        var fastest = await _db.ExecuteReaderAsync(sql,
                            new NpgsqlParameter("@todayStart", todayStart),
                            new NpgsqlParameter("@todayEnd", todayEnd));

                        if (fastest.Any())
                        {
                            stats.FastestServicePoint = new ServicePointPerformanceDto
                            {
                                Name = fastest[0]["name"].ToString() ?? "N/A",
                                AvgServiceTime = Math.Round(Convert.ToDouble(fastest[0]["avg_service_time"]) / 60.0, 1)
                            };
                        }
                    }),

                    // 12. Most Active Staff
                    Task.Run(async () =>
                    {
                        var sql = @"
                            SELECT
                                u.username as name,
                                COUNT(t.id) as tickets_served
                            FROM users u
                            INNER JOIN tickets t ON u.id = t.served_by_user_id
                            WHERE t.status = 'Finished'
                                AND t.created_at >= @todayStart
                                AND t.created_at <= @todayEnd
                            GROUP BY u.id, u.username
                            ORDER BY tickets_served DESC
                            LIMIT 1";

                        var active = await _db.ExecuteReaderAsync(sql,
                            new NpgsqlParameter("@todayStart", todayStart),
                            new NpgsqlParameter("@todayEnd", todayEnd));

                        if (active.Any())
                        {
                            stats.MostActiveStaff = new StaffPerformanceDto
                            {
                                Name = active[0]["name"].ToString() ?? "N/A",
                                TicketsServed = Convert.ToInt32(active[0]["tickets_served"])
                            };
                        }
                    }),

                    // 13. Busiest Service
                    Task.Run(async () =>
                    {
                        var sql = @"
                            SELECT
                                s.name,
                                COUNT(t.id) as ticket_count
                            FROM services s
                            INNER JOIN tickets t ON s.id = t.service_id
                            WHERE t.created_at >= @todayStart
                                AND t.created_at <= @todayEnd
                            GROUP BY s.id, s.name
                            ORDER BY ticket_count DESC
                            LIMIT 1";

                        var busiest = await _db.ExecuteReaderAsync(sql,
                            new NpgsqlParameter("@todayStart", todayStart),
                            new NpgsqlParameter("@todayEnd", todayEnd));

                        if (busiest.Any())
                        {
                            stats.BusiestService = new ServicePerformanceDto
                            {
                                Name = busiest[0]["name"].ToString() ?? "N/A",
                                TicketCount = Convert.ToInt32(busiest[0]["ticket_count"])
                            };
                        }
                    })
                };

                // Wait for all queries to complete
                await Task.WhenAll(tasks);

                // Calculate percentages for TicketsByService
                if (stats.TicketsByService.Any())
                {
                    var totalTickets = stats.TicketsByService.Sum(t => t.TicketCount);
                    if (totalTickets > 0)
                    {
                        foreach (var service in stats.TicketsByService)
                        {
                            service.Percentage = Math.Round((service.TicketCount / (double)totalTickets) * 100, 1);
                        }
                    }
                }

                stats.Success = true;
                return stats;
            }
            catch (Exception ex)
            {
                // Log the actual error (you should use ILogger in production)
                Console.WriteLine($"Dashboard Error: {ex.Message}\n{ex.StackTrace}");

                // Return default stats with error indication
                stats.Success = false;
                return stats;
            }
        }

        // Helper method to assign colors based on service ID
        private string GetColorForService(int serviceId)
        {
            return serviceId switch
            {
                1 => "#4A90E2", // Doctor Consultation - Blue
                2 => "#50E3C2", // Lab Tests - Teal
                3 => "#F5A623", // Pharmacy - Orange
                4 => "#B8E986", // Registration - Green
                5 => "#D0021B", // Emergency - Red
                6 => "#9013FE", // X-Ray - Purple
                7 => "#8B572A", // Ultrasound - Brown
                8 => "#417505", // Billing - Dark Green
                _ => "#9B9B9B"  // Default - Gray
            };
        }

        public async Task<AnalyticalReportDto> GetAnalyticalReportData(DateTime startDate, DateTime endDate)
        {
            var report = new AnalyticalReportDto
            {
                StartDate = startDate,
                EndDate = endDate,
                GeneratedAt = DateTime.UtcNow
            };

            var start = startDate.Date.ToUniversalTime();
            var end = endDate.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

            try
            {
                // 1. Get Service Point Performance
                var servicePointSql = @"
            SELECT
                sp.id,
                sp.name,
                COUNT(t.id) FILTER (WHERE t.status = 'Finished') as customers_served,
                COALESCE(
                    AVG(t.waiting_time_seconds) FILTER (WHERE t.status = 'Finished' AND t.waiting_time_seconds > 0),
                    0
                ) as avg_wait_seconds,
                COALESCE(
                    AVG(t.service_time_seconds) FILTER (WHERE t.status = 'Finished' AND t.service_time_seconds > 0),
                    0
                ) as avg_service_seconds
            FROM service_points sp
            LEFT JOIN tickets t ON sp.id = t.service_point_id
                AND t.created_at >= @start
                AND t.created_at <= @end
            GROUP BY sp.id, sp.name
            ORDER BY customers_served DESC";

                var servicePointData = await _db.ExecuteReaderAsync(servicePointSql,
                    new NpgsqlParameter("@start", start),
                    new NpgsqlParameter("@end", end));

                report.ServicePointPerformance = servicePointData.Select(row => new ServicePointReportDto
                {
                    ServicePointId = Convert.ToInt32(row["id"]),
                    ServicePointName = row["name"].ToString() ?? "Unknown",
                    CustomersServed = Convert.ToInt32(row["customers_served"]),
                    AverageWaitTimeMinutes = Math.Round(Convert.ToDouble(row["avg_wait_seconds"]) / 60.0, 1),
                    AverageServiceTimeMinutes = Math.Round(Convert.ToDouble(row["avg_service_seconds"]) / 60.0, 1)
                }).ToList();

                // 2. Get Staff/Provider Performance
                var staffSql = @"
            SELECT
                u.id,
                u.username,
                sp.name as service_point_name,
                COUNT(t.id) FILTER (WHERE t.status = 'Finished') as customers_served,
                COALESCE(
                    AVG(t.waiting_time_seconds) FILTER (WHERE t.status = 'Finished' AND t.waiting_time_seconds > 0),
                    0
                ) as avg_wait_seconds,
                COALESCE(
                    AVG(t.service_time_seconds) FILTER (WHERE t.status = 'Finished' AND t.service_time_seconds > 0),
                    0
                ) as avg_service_seconds
            FROM users u
            LEFT JOIN service_points sp ON u.service_point_id = sp.id
            LEFT JOIN tickets t ON u.id = t.served_by_user_id
                AND t.created_at >= @start
                AND t.created_at <= @end
            WHERE u.role = 'Staff'
            GROUP BY u.id, u.username, sp.name
            ORDER BY customers_served DESC";

                var staffData = await _db.ExecuteReaderAsync(staffSql,
                    new NpgsqlParameter("@start", start),
                    new NpgsqlParameter("@end", end));

                report.StaffPerformance = staffData.Select(row => new StaffReportDto
                {
                    StaffId = Convert.ToInt32(row["id"]),
                    StaffName = row["username"].ToString() ?? "Unknown",
                    ServicePointName = row["service_point_name"]?.ToString() ?? "Not Assigned",
                    CustomersServed = Convert.ToInt32(row["customers_served"]),
                    AverageWaitTimeMinutes = Math.Round(Convert.ToDouble(row["avg_wait_seconds"]) / 60.0, 1),
                    AverageServiceTimeMinutes = Math.Round(Convert.ToDouble(row["avg_service_seconds"]) / 60.0, 1)
                }).ToList();

                // 3. Get Overall Summary
                var summarySql = @"
            SELECT
                COUNT(*) FILTER (WHERE status = 'Finished') as total_served,
                COALESCE(
                    AVG(waiting_time_seconds) FILTER (WHERE status = 'Finished' AND waiting_time_seconds > 0),
                    0
                ) as overall_avg_wait,
                COALESCE(
                    AVG(service_time_seconds) FILTER (WHERE status = 'Finished' AND service_time_seconds > 0),
                    0
                ) as overall_avg_service
            FROM tickets
            WHERE created_at >= @start
            AND created_at <= @end";

                var summaryData = await _db.ExecuteReaderAsync(summarySql,
                    new NpgsqlParameter("@start", start),
                    new NpgsqlParameter("@end", end));

                if (summaryData.Any())
                {
                    var row = summaryData[0];
                    report.TotalCustomersServed = Convert.ToInt32(row["total_served"]);
                    report.OverallAverageWaitTimeMinutes = Math.Round(Convert.ToDouble(row["overall_avg_wait"]) / 60.0, 1);
                    report.OverallAverageServiceTimeMinutes = Math.Round(Convert.ToDouble(row["overall_avg_service"]) / 60.0, 1);
                }

                report.Success = true;
                return report;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Report Generation Error: {ex.Message}\n{ex.StackTrace}");
                report.Success = false;
                return report;
            }
        }
    }
}