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
        public DbSet<CheckSubmission> CheckSubmissions => Set<CheckSubmission>();
        public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
        public DbSet<CheckReview> CheckReviews => Set<CheckReview>();

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

            // Existing rows get these values when the columns are added.
            builder.Entity<ProjectAssignment>()
                .Property(a => a.Role)
                .HasDefaultValue(ProjectOptions.RoleEngineer);
            builder.Entity<ProjectTimeEntry>()
                .Property(t => t.WorkType)
                .HasDefaultValue(ProjectOptions.WorkModeling);
            builder.Entity<ProjectSubcategory>()
                .Property(s => s.Status)
                .HasDefaultValue(ProjectOptions.StatusInProgress);

            builder.Entity<CheckSubmission>().HasIndex(c => new { c.ProjectId, c.SubcategoryId });
            builder.Entity<CheckSubmission>().HasIndex(c => c.CheckerId);
            builder.Entity<CheckSubmission>()
                .HasOne(c => c.Subcategory)
                .WithMany()
                .HasForeignKey(c => c.SubcategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // Tasks: removing a task never removes hours or check history.
            builder.Entity<ProjectTask>()
                .HasOne(t => t.Subcategory)
                .WithMany(s => s.Tasks)
                .HasForeignKey(t => t.SubcategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<ProjectTask>()
                .HasOne(t => t.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.NoAction);
            builder.Entity<ProjectTask>()
                .Property(t => t.Status)
                .HasDefaultValue(ProjectOptions.StatusInProgress);
            builder.Entity<ProjectTimeEntry>()
                .HasOne(t => t.Task)
                .WithMany()
                .HasForeignKey(t => t.TaskId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.Entity<CheckSubmission>()
                .HasOne(c => c.Task)
                .WithMany()
                .HasForeignKey(c => c.TaskId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.Entity<CheckReview>()
                .HasOne(r => r.Submission)
                .WithMany(c => c.Reviews)
                .HasForeignKey(r => r.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}