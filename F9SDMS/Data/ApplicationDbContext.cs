using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<ProjectAssignment> ProjectAssignments => Set<ProjectAssignment>();
        public DbSet<ProjectTimeEntry> ProjectTimeEntries => Set<ProjectTimeEntry>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // One assignment per person per project.
            builder.Entity<ProjectAssignment>()
                .HasIndex(a => new { a.ProjectId, a.EmployeeId })
                .IsUnique();

            builder.Entity<ProjectAssignment>().HasIndex(a => a.EmployeeId);
            builder.Entity<ProjectTimeEntry>().HasIndex(t => new { t.EmployeeId, t.EndTime });
        }
    }
}