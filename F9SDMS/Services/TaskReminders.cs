using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    /// <summary>
    /// Task deadlines: the "Due tomorrow" / "Overdue" chips, and the bell reminders sent to a
    /// task's assignees and its project manager the working day before the deadline.
    /// </summary>
    public static class TaskReminders
    {
        /// <summary>
        /// The day the reminder goes out: the day before the deadline, moved back to Friday when
        /// that falls on a weekend (a Monday deadline reminds on the Friday before).
        /// </summary>
        public static DateTime ReminderDay(DateTime deadline)
        {
            var day = deadline.Date.AddDays(-1);
            while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                day = day.AddDays(-1);
            }
            return day;
        }

        /// <summary>
        /// How close a deadline is, for the chips: "done", "overdue", "today", "tomorrow", "soon"
        /// (within 3 days) or "later". Null when there's no deadline.
        /// </summary>
        public static string? DueKind(DateTime? deadline, DateTime today, string status)
        {
            if (deadline is not DateTime d) return null;
            if (status == ProjectOptions.StatusApproved) return "done";
            var days = (d.Date - today.Date).Days;
            return days switch
            {
                < 0 => "overdue",
                0 => "today",
                1 => "tomorrow",
                <= 3 => "soon",
                _ => "later"
            };
        }

        /// <summary>Chip text, e.g. "Due tomorrow · Oct 17", "Overdue · Oct 15", "Oct 24".</summary>
        public static string DueLabel(DateTime deadline, DateTime today, string status)
        {
            var date = deadline.ToString("MMM d");
            return DueKind(deadline, today, status) switch
            {
                "overdue" => $"Overdue · {date}",
                "today" => $"Due today · {date}",
                "tomorrow" => $"Due tomorrow · {date}",
                _ => date
            };
        }

        /// <summary>Gives every task without a deadline its project's due date (tasks made before deadlines existed).</summary>
        public static async Task BackfillDeadlinesAsync(ApplicationDbContext db)
        {
            var tasks = await db.ProjectTasks
                .Where(t => t.Deadline == null)
                .Include(t => t.Project)
                .ToListAsync();
            if (tasks.Count == 0) return;

            foreach (var task in tasks)
            {
                if (task.Project is not null)
                {
                    task.Deadline = task.Project.DueDate.Date;
                }
            }
            await db.SaveChangesAsync();
        }

        /// <summary>
        /// Creates the reminders that are due (for one task, or all) and removes unread ones that
        /// no longer apply (task approved or archived, deadline changed, person unassigned).
        /// </summary>
        public static async Task SyncAsync(ApplicationDbContext db, DateTime now, int? onlyTaskId = null)
        {
            var today = now.Date;

            var query = db.ProjectTasks.AsNoTracking()
                .Include(t => t.Project)
                .Include(t => t.Subcategory)
                .Include(t => t.Assignees)
                .Where(t => t.Deadline != null);
            if (onlyTaskId is int id)
            {
                query = query.Where(t => t.Id == id);
            }
            else
            {
                // Only tasks that can still need a reminder.
                query = query.Where(t => !t.IsArchived && t.Status != ProjectOptions.StatusApproved);
            }
            var tasks = await query.ToListAsync();

            var taskIds = tasks.Select(t => t.Id).ToList();
            var existing = onlyTaskId is int one
                ? await db.Notifications.Where(n => n.TaskId == one).ToListAsync()
                : await db.Notifications.Where(n => n.TaskId != null && (n.ReadAt == null || taskIds.Contains(n.TaskId.Value))).ToListAsync();

            // Unread reminders for tasks that are gone, approved, archived or have no deadline.
            foreach (var n in existing.Where(n => n.ReadAt == null && !taskIds.Contains(n.TaskId!.Value)).ToList())
            {
                db.Notifications.Remove(n);
                existing.Remove(n);
            }

            var names = await ProjectChecking.GetNamesAsync(db);
            string FirstName(string userId) => names.TryGetValue(userId, out var n) ? n.Split(' ')[0] : "Someone";

            foreach (var task in tasks)
            {
                var deadline = task.Deadline!.Value.Date;
                var project = task.Project;
                var active = !task.IsArchived
                    && task.Subcategory?.IsArchived != true
                    && task.Status != ProjectOptions.StatusApproved
                    && project is not null
                    && project.Status != ProjectOptions.StatusCompleted;

                var assignees = task.Assignees.Select(a => a.EmployeeId).Distinct().ToList();
                var managerId = project?.ManagerId ?? project?.CreatedById;
                var recipients = assignees.ToList();
                if (managerId is not null && !recipients.Contains(managerId)) recipients.Add(managerId);

                string? kind = null;
                if (active)
                {
                    if (today > deadline) kind = Notification.KindOverdue;
                    else if (today >= ReminderDay(deadline)) kind = Notification.KindDueSoon;
                }

                // Remove unread reminders for this task that don't apply anymore.
                foreach (var n in existing.Where(n => n.TaskId == task.Id && n.ReadAt == null).ToList())
                {
                    var stillValid = active
                        && n.DueDate == deadline
                        && recipients.Contains(n.UserId)
                        && (n.Kind == kind || (n.Kind == Notification.KindDueSoon && kind == Notification.KindOverdue));
                    if (!stillValid)
                    {
                        db.Notifications.Remove(n);
                        existing.Remove(n);
                    }
                }

                if (kind is null || project is null) continue;

                var title = task.Subcategory is null
                    ? $"{project.Sample} › {task.Name}"
                    : $"{project.Sample} › {task.Subcategory.Name} › {task.Name}";
                var assignedText = assignees.Count == 0 ? "No one assigned" : string.Join(", ", assignees.Select(FirstName));

                foreach (var userId in recipients)
                {
                    if (existing.Any(n => n.UserId == userId && n.TaskId == task.Id && n.Kind == kind && n.DueDate == deadline))
                    {
                        continue;
                    }

                    var when = kind == Notification.KindOverdue
                        ? $"Was due {deadline:ddd, MMM d}"
                        : (deadline - today).Days switch
                        {
                            0 => $"Due today, {deadline:MMM d}",
                            1 => $"Due tomorrow, {deadline:ddd, MMM d}",
                            _ => $"Due {deadline:ddd, MMM d}"
                        };
                    var who = userId == managerId && !assignees.Contains(userId)
                        ? assignedText
                        : (assignees.Count > 1 ? "Assigned to you and " + string.Join(", ", assignees.Where(a => a != userId).Select(FirstName)) : "Assigned to you");

                    var notification = new Notification
                    {
                        UserId = userId,
                        Kind = kind,
                        TaskId = task.Id,
                        ProjectId = project.Id,
                        DueDate = deadline,
                        Title = title,
                        Body = $"{when} · {who} · {task.Status}",
                        Link = $"/projects/{project.Id}",
                        CreatedAt = now
                    };
                    db.Notifications.Add(notification);
                    existing.Add(notification);
                }
            }

            await db.SaveChangesAsync();
        }

        public static async Task<int> CountUnreadAsync(ApplicationDbContext db, string userId) =>
            await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null);
    }

    /// <summary>Checks task deadlines every few minutes and sends the bell reminders.</summary>
    public class DeadlineReminderService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DeadlineReminderService> _logger;

        public DeadlineReminderService(IServiceScopeFactory scopeFactory, ILogger<DeadlineReminderService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Let the app finish starting (and the database update run) first.
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var clock = scope.ServiceProvider.GetRequiredService<IAppClock>();
                    await TaskReminders.SyncAsync(db, clock.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while sending task deadline reminders.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
    }
}
