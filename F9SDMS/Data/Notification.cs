using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>A message in someone's notification bell, e.g. "Due tomorrow" for a task.</summary>
    public class Notification
    {
        public const string KindDueSoon = "DueSoon";
        public const string KindOverdue = "Overdue";
        public const string KindRequestApproved = "RequestApproved";
        public const string KindRequestRejected = "RequestRejected";
        public const string KindTicketNew = "TicketNew";
        public const string KindTicketReply = "TicketReply";
        public const string KindTicketResolved = "TicketResolved";

        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>A task reminder (<see cref="KindDueSoon"/>, <see cref="KindOverdue"/>) or a decision on the person's overtime or leave request.</summary>
        [Required, MaxLength(30)]
        public string Kind { get; set; } = KindDueSoon;

        public int? TaskId { get; set; }

        public int? ProjectId { get; set; }

        /// <summary>The task deadline this reminder is about (a new deadline gets a new reminder).</summary>
        public DateTime? DueDate { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Body { get; set; }

        [MaxLength(300)]
        public string? Link { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ReadAt { get; set; }
    }
}
