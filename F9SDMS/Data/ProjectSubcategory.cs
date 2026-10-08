using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>
    /// A part of a project (e.g. "Tabuk Golf" under "Tabuk") that hours are logged against,
    /// so a project's manhours aren't lumped together.
    /// </summary>
    public class ProjectSubcategory
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public Project? Project { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Archived sub-categories are hidden from employees but keep their hours.</summary>
        public bool IsArchived { get; set; }

        /// <summary>
        /// Checking status of this part: In Progress, For Checking, Returned or Approved
        /// (see <see cref="ProjectOptions"/>).
        /// </summary>
        [Required, MaxLength(30)]
        public string Status { get; set; } = ProjectOptions.StatusInProgress;

        public DateTime CreatedAt { get; set; }

        public List<ProjectTask> Tasks { get; set; } = new();
    }
}
