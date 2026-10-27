using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>An IT support ticket filed by an employee and handled by the admin account.</summary>
    public class Ticket
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Who filed it.</summary>
        [Required]
        public string EmployeeId { get; set; } = string.Empty;

        /// <summary>One of <see cref="TicketOptions.Categories"/>.</summary>
        [Required, MaxLength(40)]
        public string Category { get; set; } = "Other";

        /// <summary>One of <see cref="TicketOptions.Priorities"/>.</summary>
        [Required, MaxLength(20)]
        public string Priority { get; set; } = TicketOptions.PriorityNormal;

        [Required, MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        /// <summary>One of <see cref="TicketOptions.Statuses"/>.</summary>
        [Required, MaxLength(20)]
        public string Status { get; set; } = TicketOptions.StatusOpen;

        public DateTime CreatedAt { get; set; }

        /// <summary>Last time anything happened on the ticket (message or status change).</summary>
        public DateTime UpdatedAt { get; set; }

        public DateTime? ResolvedAt { get; set; }

        public List<TicketMessage> Messages { get; set; } = new();

        /// <summary>Display code, e.g. "T-0042". Not stored.</summary>
        public string Code => FormatCode(Id);

        public static string FormatCode(int id) => $"T-{id:D4}";
    }
}
