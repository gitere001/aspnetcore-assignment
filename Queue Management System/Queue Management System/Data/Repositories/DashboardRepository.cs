using Queue_Management_System.Data;
using Queue_Management_System.Models;
using Npgsql;

namespace Queue_Management_System.Data.Repositories
{
    public class DashboardRepository
    {
        private readonly DatabaseService _db;

        public DashboardRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task<DashboardStatsDto> GetDashboardStats(DateTime date)
        {
            var stats = new DashboardStatsDto();
            stats.Timestamp = DateTime.UtcNow;

            // Get date ranges with timezone consideration
            var todayStart = date.Date.ToUniversalTime();
            var todayEnd = date.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
            var yesterdayStart = date.Date.AddDays(-1).ToUniversalTime();
            var yesterdayEnd = date.Date.AddTicks(-1).ToUniversalTime();

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

                    // 4. Active Service Points (FIXED - based on your actual data structure)
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
                            WHERE status = 'Waiting'";

                        var count = await _db.ExecuteCountAsync(sql);
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

                    // 8. Hourly Tickets (for chart)
                    Task.Run(async () =>
                    {
                        var sql = @"
                            WITH hours AS (
                                SELECT generate_series(0, 23) as hour
                            ),
                            served_hourly AS (
                                SELECT
                                    EXTRACT(HOUR FROM created_at) as hour,
                                    COUNT(*) as served
                                FROM tickets
                                WHERE status = 'Finished'
                                    AND created_at >= @todayStart
                                    AND created_at <= @todayEnd
                                GROUP BY EXTRACT(HOUR FROM created_at)
                            ),
                            waiting_now AS (
                                SELECT COUNT(*) as waiting
                                FROM tickets
                                WHERE status = 'Waiting'
                            )
                            SELECT
                                h.hour,
                                COALESCE(sh.served, 0) as served,
                                COALESCE((SELECT waiting FROM waiting_now), 0) as waiting
                            FROM hours h
                            LEFT JOIN served_hourly sh ON h.hour = sh.hour
                            ORDER BY h.hour";

                        var hourlyData = await _db.ExecuteReaderAsync(sql,
                            new NpgsqlParameter("@todayStart", todayStart),
                            new NpgsqlParameter("@todayEnd", todayEnd));

                        stats.HourlyTickets = hourlyData.Select(row => new HourlyTicketsDto
                        {
                            Hour = $"{Convert.ToInt32(row["hour"]):00}:00",
                            Served = Convert.ToInt32(row["served"]),
                            Waiting = Convert.ToInt32(row["waiting"])
                        }).ToList();
                    }),

                    // 9. Fastest Service Point
                    Task.Run(async () =>
                    {
                        var sql = @"
                            SELECT
                                sp.name as service_point_name,
                                COALESCE(AVG(t.service_time_seconds), 0) as avg_time
                            FROM service_points sp
                            LEFT JOIN tickets t ON sp.id = t.service_point_id
                                AND t.status = 'Finished'
                                AND t.service_time_seconds > 0
                                AND t.created_at >= @todayStart
                                AND t.created_at <= @todayEnd
                            WHERE sp.is_active = true
                            GROUP BY sp.id, sp.name
                            HAVING COUNT(t.id) > 0
                            ORDER BY avg_time ASC
                            LIMIT 1";

                        var fastest = await _db.ExecuteReaderAsync(sql,
                            new NpgsqlParameter("@todayStart", todayStart),
                            new NpgsqlParameter("@todayEnd", todayEnd));

                        if (fastest.Any())
                        {
                            stats.FastestServicePoint = new ServicePointPerformanceDto
                            {
                                Name = fastest[0]["service_point_name"].ToString() ?? "N/A",
                                AvgServiceTime = Convert.ToInt32(fastest[0]["avg_time"]) / 60 // Convert to minutes
                            };
                        }
                    }),

                    // 10. Most Active Staff
                    Task.Run(async () =>
                    {
                        var sql = @"
                            SELECT
                                u.username as staff_name,
                                COUNT(t.id) as tickets_served
                            FROM users u
                            LEFT JOIN tickets t ON u.id = t.served_by_user_id
                                AND t.status = 'Finished'
                                AND t.created_at >= @todayStart
                                AND t.created_at <= @todayEnd
                            WHERE u.role = 'Staff'
                            GROUP BY u.id, u.username
                            ORDER BY tickets_served DESC
                            LIMIT 1";

                        var mostActive = await _db.ExecuteReaderAsync(sql,
                            new NpgsqlParameter("@todayStart", todayStart),
                            new NpgsqlParameter("@todayEnd", todayEnd));

                        if (mostActive.Any())
                        {
                            stats.MostActiveStaff = new StaffPerformanceDto
                            {
                                Name = mostActive[0]["staff_name"].ToString() ?? "N/A",
                                TicketsServed = Convert.ToInt32(mostActive[0]["tickets_served"])
                            };
                        }
                    }),

                    // 11. Busiest Service
                    Task.Run(async () =>
                    {
                        var sql = @"
                            SELECT
                                s.name as service_name,
                                COUNT(t.id) as ticket_count
                            FROM services s
                            LEFT JOIN tickets t ON s.id = t.service_id
                                AND t.status IN ('Called', 'Serving', 'Finished', 'NoShow')
                                AND t.created_at >= @todayStart
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
                                Name = busiest[0]["service_name"].ToString() ?? "N/A",
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