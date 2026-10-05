using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>A BIM project that managers create and assign to engineers/architects.</summary>
    public class Project
    {
        [Key]
        public int Id { get; set; }

        /// <summary>The sample / project name.</summary>
        [Required, MaxLength(200)]
        public string Sample { get; set; } = string.Empty;

        /// <summary>One of <see cref="ProjectOptions.LodOptions"/>.</summary>
        [MaxLength(50)]
        public string? Lod { get; set; }

        /// <summary>One of <see cref="ProjectOptions.CategoryOptions"/>.</summary>
        [MaxLength(50)]
        public string? Category { get; set; }

        public DateTime DueDate { get; set; }

        [MaxLength(4000)]
        public string? Notes { get; set; }

        /// <summary>One of <see cref="ProjectOptions.Statuses"/>.</summary>
        [Required, MaxLength(30)]
        public string Status { get; set; } = ProjectOptions.StatusUnassigned;

        public DateTime CreatedAt { get; set; }

        public string? CreatedById { get; set; }

        public List<ProjectAssignment> Assignments { get; set; } = new();

        public List<ProjectTimeEntry> TimeEntries { get; set; } = new();

        public List<ProjectSubcategory> Subcategories { get; set; } = new();

        /// <summary>Display code, e.g. "P-0012". Not stored.</summary>
        public string Code => FormatCode(Id);

        public static string FormatCode(int id) => $"P-{id:D4}";
    }
}
