using System.ComponentModel.DataAnnotations;

namespace Queue_Management_System.Models
{
    public class ServicePoint
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        public int ServiceId { get; set; }

        public bool IsActive { get; set; } = true; // Can be enabled/disabled

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string ServiceName { get; set; } = string.Empty; // "Doctor Consultation"
        public string ServicePrefix { get; set; } = string.Empty; // "DOC-"
    }
}