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
    }
}