using System.ComponentModel.DataAnnotations;

namespace Queue_Management_System.Models
{
    public class Ticket
    {
        public int Id { get; set; }

        [Required]
        public string TicketNumber { get; set; } = string.Empty; // e.g., "D001"

        public int ServiceId { get; set; } // Foreign key to Service table

        public int? ServicePointId { get; set; } // Which counter is serving (nullable - not assigned initially)

        [Required]
        public string Status { get; set; } = "Waiting"; // Waiting, Called, Serving, Finished, NoShow

        public DateTime CreatedAt { get; set; } = DateTime.Now; // When customer got ticket

        public DateTime? CalledAt { get; set; } // When staff called the customer

        public DateTime? FinishedAt { get; set; } // When service completed

        // Calculated fields for reports
        public int? WaitingTimeSeconds { get; set; } // CalledAt - CreatedAt

        public int? ServiceTimeSeconds { get; set; } // FinishedAt - CalledAt
        public int? ServedByUserId { get; set; } // Which staff member served the ticket
        public string? ServedByUsername { get; set; } // Staff username who served the ticket
    }
}