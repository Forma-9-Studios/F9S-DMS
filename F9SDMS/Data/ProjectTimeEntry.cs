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

        /// <summary>The sub-category worked on, when the project has sub-categories.</summary>
        public int? SubcategoryId { get; set; }

        public ProjectSubcategory? Subcategory { get; set; }

        /// <summary>The task worked on (projects with sub-categories).</summary>
        public int? TaskId { get; set; }

        public ProjectTask? Task { get; set; }

        /// <summary><see cref="ProjectOptions.WorkModeling"/> or <see cref="ProjectOptions.WorkChecking"/>.</summary>
        [Required, MaxLength(20)]
        public string WorkType { get; set; } = ProjectOptions.WorkModeling;

        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }
    }
}
