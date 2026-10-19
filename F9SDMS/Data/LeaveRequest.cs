using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>An employee's request for leave (one or more whole days, or a half day).</summary>
    public class LeaveRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string EmployeeId { get; set; } = string.Empty;

        /// <summary>One of <see cref="RequestOptions.LeaveTypes"/>.</summary>
        [Required, MaxLength(30)]
        public string LeaveType { get; set; } = RequestOptions.LeaveVacation;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public bool IsHalfDay { get; set; }

        /// <summary>"AM" or "PM" for a half day.</summary>
        [MaxLength(2)]
        public string? HalfDayPart { get; set; }

        /// <summary>Working days (Mon–Fri) the leave covers: 0.5 for a half day.</summary>
        public double TotalDays { get; set; }

        /// <summary>Days taken from the Vacation or Sick credits.</summary>
        public double CreditDays { get; set; }

        /// <summary>Days beyond the available credits, filed as unpaid (all days for Unpaid leave).</summary>
        public double UnpaidDays { get; set; }

        [Required, MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Status { get; set; } = RequestOptions.StatusPending;

        public DateTime CreatedAt { get; set; }

        public string? DecidedById { get; set; }
        public DateTime? DecidedAt { get; set; }

        [MaxLength(1000)]
        public string? DecisionNote { get; set; }
    }
}
