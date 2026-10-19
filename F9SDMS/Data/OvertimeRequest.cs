using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>An employee's request to work (or record) hours beyond the regular 8.</summary>
    public class OvertimeRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string EmployeeId { get; set; } = string.Empty;

        /// <summary><see cref="RequestOptions.OvertimePlanned"/> or <see cref="RequestOptions.OvertimeWorked"/>.</summary>
        [Required, MaxLength(30)]
        public string Kind { get; set; } = RequestOptions.OvertimePlanned;

        /// <summary>The day the overtime is (or was) worked. Time part is always midnight.</summary>
        public DateTime Date { get; set; }

        /// <summary>Planned overtime only: when it starts and ends.</summary>
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }

        /// <summary>Length of the overtime in minutes.</summary>
        public int Minutes { get; set; }

        /// <summary>Optional project the overtime is for.</summary>
        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required, MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Status { get; set; } = RequestOptions.StatusPending;

        public DateTime CreatedAt { get; set; }

        public string? DecidedById { get; set; }
        public DateTime? DecidedAt { get; set; }

        /// <summary>Required when rejecting; shown to the employee.</summary>
        [MaxLength(1000)]
        public string? DecisionNote { get; set; }
    }
}
