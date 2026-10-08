using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>
    /// A piece of work under a sub-category (e.g. "Lighting layout" under Tabuk › FEC).
    /// Engineers pick a task as their status, so hours are logged per task, and each task is
    /// checked on its own (engineer → checker → project manager). A sub-category is approved
    /// once all of its tasks are approved.
    /// </summary>
    public class ProjectTask
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public Project? Project { get; set; }

        public int SubcategoryId { get; set; }

        public ProjectSubcategory? Subcategory { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Archived tasks are hidden from employees but keep their hours and history.</summary>
        public bool IsArchived { get; set; }

        /// <summary>In Progress, For Checking, Manager Check, Returned by Manager, Returned or Approved.</summary>
        [Required, MaxLength(30)]
        public string Status { get; set; } = ProjectOptions.StatusInProgress;

        public DateTime CreatedAt { get; set; }
    }
}
