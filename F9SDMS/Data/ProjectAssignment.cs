using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>Links a project to one assigned BIM engineer / architect.</summary>
    public class ProjectAssignment
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public Project? Project { get; set; }

        [Required]
        public string EmployeeId { get; set; } = string.Empty;
    }
}
