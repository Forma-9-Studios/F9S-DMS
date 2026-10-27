using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>
    /// One entry in a ticket's conversation: the employee's description, a reply, an admin's
    /// internal note (never shown to the employee) or a status change line.
    /// </summary>
    public class TicketMessage
    {
        [Key]
        public int Id { get; set; }

        public int TicketId { get; set; }

        public Ticket? Ticket { get; set; }

        /// <summary>Who wrote it. Null for system lines (e.g. "Closed automatically").</summary>
        public string? AuthorId { get; set; }

        [MaxLength(4000)]
        public string Body { get; set; } = string.Empty;

        /// <summary>Admin-only note.</summary>
        public bool IsInternal { get; set; }

        /// <summary>A status/priority change line rather than a message.</summary>
        public bool IsEvent { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>Optional attachment (screenshot or file), stored by <see cref="Services.TicketFileStore"/>.</summary>
        [MaxLength(80)]
        public string? AttachmentStoredName { get; set; }

        [MaxLength(255)]
        public string? AttachmentFileName { get; set; }

        public long? AttachmentSize { get; set; }
    }
}
