using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    public class AttendanceSession
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string EmployeeId { get; set; } = string.Empty;

        [Required]
        public DateTime ClockInTime { get; set; }

        public DateTime? ClockOutTime { get; set; }

        /// <summary>
        /// Last time the employee's dashboard was open (updated about once a minute).
        /// Null for sessions recorded before the heartbeat existed.
        /// </summary>
        public DateTime? LastSeenTime { get; set; }

        /// <summary>
        /// Overtime: the employee logged in again after their regular 8 hours for the day.
        /// Overtime sessions have no time limit (they still close when the dashboard is left closed).
        /// </summary>
        public bool IsOvertime { get; set; }

        /// <summary>
        /// Regular sessions: how long this session may run, which is what's left of the day's
        /// 8 hours when it started. Null for older sessions (8 hours).
        /// </summary>
        public int? LimitMinutes { get; set; }
    }
}