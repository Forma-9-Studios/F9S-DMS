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
        public DbSet<ProjectSubcategory> ProjectSubcategories => Set<ProjectSubcategory>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // One assignment per person per project.
            builder.Entity<ProjectAssignment>()
                .HasIndex(a => new { a.ProjectId, a.EmployeeId })
                .IsUnique();

            builder.Entity<ProjectAssignment>().HasIndex(a => a.EmployeeId);
            builder.Entity<ProjectTimeEntry>().HasIndex(t => new { t.EmployeeId, t.EndTime });

            // Removing a sub-category never removes logged hours (the app only allows
            // removing ones without hours; the rest are archived).
            builder.Entity<ProjectTimeEntry>()
                .HasOne(t => t.Subcategory)
                .WithMany()
                .HasForeignKey(t => t.SubcategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}