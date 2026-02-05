namespace Queue_Management_System.Models
{
    public class AnalyticalReportDto
    {
        public bool Success { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime GeneratedAt { get; set; }

        // Overall Summary
        public int TotalCustomersServed { get; set; }
        public double OverallAverageWaitTimeMinutes { get; set; }
        public double OverallAverageServiceTimeMinutes { get; set; }

        // Detailed Performance
        public List<ServicePointReportDto> ServicePointPerformance { get; set; } = new();
        public List<StaffReportDto> StaffPerformance { get; set; } = new();
    }

    public class ServicePointReportDto
    {
        public int ServicePointId { get; set; }
        public string ServicePointName { get; set; } = string.Empty;
        public int CustomersServed { get; set; }
        public double AverageWaitTimeMinutes { get; set; }
        public double AverageServiceTimeMinutes { get; set; }
    }

    public class StaffReportDto
    {
        public int StaffId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public string ServicePointName { get; set; } = string.Empty;
        public int CustomersServed { get; set; }
        public double AverageWaitTimeMinutes { get; set; }
        public double AverageServiceTimeMinutes { get; set; }
    }
}