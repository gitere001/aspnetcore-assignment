using System;
using System.Collections.Generic;

namespace Queue_Management_System.Models
{
    public class DashboardStatsDto
    {
        public bool Success { get; set; } = true;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string TimeRange { get; set; } = "Today";

        // Main Stats
        public int TotalServed { get; set; }
        public double AverageWaitTime { get; set; }
        public int ActiveServicePoints { get; set; }
        public int CustomersWaiting { get; set; }
        public double ServiceEfficiency { get; set; }

        // Comparisons
        public double ServedVsYesterday { get; set; }
        public double WaitTimeVsYesterday { get; set; }
        public double EfficiencyVsYesterday { get; set; }

        // Charts Data
        public List<ServiceTicketsDto> TicketsByService { get; set; } = new();
        public List<HourlyTicketsDto> HourlyTickets { get; set; } = new();

        // Top Performers
        public ServicePointPerformanceDto FastestServicePoint { get; set; } = new();
        public StaffPerformanceDto MostActiveStaff { get; set; } = new();
        public ServicePerformanceDto BusiestService { get; set; } = new();
    }

    public class ServiceTicketsDto
    {
        public string ServiceName { get; set; } = string.Empty;
        public int TicketCount { get; set; }
        public double Percentage { get; set; }
        public string Color { get; set; } = string.Empty;
    }

    public class HourlyTicketsDto
    {
        public string Hour { get; set; } = string.Empty;
        public int Served { get; set; }
        public int Waiting { get; set; }
    }

    public class ServicePointPerformanceDto
    {
        public string Name { get; set; } = string.Empty;
        public int AvgServiceTime { get; set; }
    }

    public class StaffPerformanceDto
    {
        public string Name { get; set; } = string.Empty;
        public int TicketsServed { get; set; }
    }

    public class ServicePerformanceDto
    {
        public string Name { get; set; } = string.Empty;
        public int TicketCount { get; set; }
    }
}