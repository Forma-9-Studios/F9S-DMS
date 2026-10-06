using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>Links a project to one assigned BIM engineer / architect, or to one of its checkers.</summary>
    public class ProjectAssignment
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public Project? Project { get; set; }

        [Required]
        public string EmployeeId { get; set; } = string.Empty;

        /// <summary><see cref="ProjectOptions.RoleEngineer"/> or <see cref="ProjectOptions.RoleChecker"/>.</summary>
        [Required, MaxLength(20)]
        public string Role { get; set; } = ProjectOptions.RoleEngineer;
    }
}
