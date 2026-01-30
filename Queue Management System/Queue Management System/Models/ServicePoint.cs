using System.ComponentModel.DataAnnotations;

namespace Queue_Management_System.Models
{
    public class ServicePoint
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty; // "Counter 1", "Counter 2"

        public bool IsActive { get; set; } = true; // Can be enabled/disabled

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}