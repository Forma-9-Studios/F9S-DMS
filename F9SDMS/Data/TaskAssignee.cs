using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>
    /// An engineer assigned to a task. Any engineer on the project can still work on any task;
    /// assignees are the ones reminded before its deadline.
    /// </summary>
    public class TaskAssignee
    {
        [Key]
        public int Id { get; set; }

        public int TaskId { get; set; }

        public ProjectTask? Task { get; set; }

        [Required]
        public string EmployeeId { get; set; } = string.Empty;
    }
}
