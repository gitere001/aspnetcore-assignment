using System.ComponentModel.DataAnnotations;
namespace Queue_Management_System.Models
{
    public class Service
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        public string PrefixCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}