using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>
    /// Time an employee spent with a project as their current status.
    /// EndTime is null while they are still working on it.
    /// </summary>
    public class ProjectTimeEntry
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public Project? Project { get; set; }

        [Required]
        public string EmployeeId { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }
    }
}
