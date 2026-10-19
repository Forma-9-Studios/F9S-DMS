using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>
    /// A manual change to an employee's leave balance by an admin, e.g. leave used before the
    /// system started. Positive adds days, negative removes them.
    /// </summary>
    public class LeaveAdjustment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string EmployeeId { get; set; } = string.Empty;

        public int Year { get; set; }

        /// <summary><see cref="RequestOptions.LeaveVacation"/> or <see cref="RequestOptions.LeaveSick"/>.</summary>
        [Required, MaxLength(30)]
        public string LeaveType { get; set; } = RequestOptions.LeaveVacation;

        public double Days { get; set; }

        [Required, MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public string? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
