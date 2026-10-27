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
        public DbSet<OvertimeRequest> OvertimeRequests => Set<OvertimeRequest>();
        public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
        public DbSet<LeaveAdjustment> LeaveAdjustments => Set<LeaveAdjustment>();
        public DbSet<LeavePolicy> LeavePolicies => Set<LeavePolicy>();
        public DbSet<TaskAssignee> TaskAssignees => Set<TaskAssignee>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();

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

            // Overtime and leave requests.
            builder.Entity<OvertimeRequest>().HasIndex(o => new { o.EmployeeId, o.Date });
            builder.Entity<OvertimeRequest>().HasIndex(o => o.Status);
            builder.Entity<OvertimeRequest>()
                .HasOne(o => o.Project)
                .WithMany()
                .HasForeignKey(o => o.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.Entity<LeaveRequest>().HasIndex(l => new { l.EmployeeId, l.StartDate });
            builder.Entity<LeaveRequest>().HasIndex(l => l.Status);
            builder.Entity<LeaveAdjustment>().HasIndex(a => new { a.EmployeeId, a.Year });

            // Task assignees and the notification bell.
            builder.Entity<TaskAssignee>()
                .HasOne(a => a.Task)
                .WithMany(t => t.Assignees)
                .HasForeignKey(a => a.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<TaskAssignee>().HasIndex(a => new { a.TaskId, a.EmployeeId }).IsUnique();
            builder.Entity<TaskAssignee>().HasIndex(a => a.EmployeeId);
            builder.Entity<Notification>().HasIndex(n => new { n.UserId, n.ReadAt });
            builder.Entity<Notification>().HasIndex(n => n.TaskId);

            // IT support tickets.
            builder.Entity<Ticket>().HasIndex(t => new { t.EmployeeId, t.Status });
            builder.Entity<Ticket>().HasIndex(t => t.Status);
            builder.Entity<TicketMessage>()
                .HasOne(m => m.Ticket)
                .WithMany(t => t.Messages)
                .HasForeignKey(m => m.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            // The company's leave policy: one row, created with the defaults.
            builder.Entity<LeavePolicy>().HasData(new LeavePolicy
            {
                Id = 1,
                VacationDaysPerYear = 15,
                SickDaysPerYear = 10,
                VacationNoticeDays = 3,
                CarryOverMaxDays = 5
            });
        }
    }
}