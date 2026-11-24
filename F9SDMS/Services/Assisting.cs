using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    /// <summary>
    /// The Assist Colleague status: pick who you're helping and which of their tasks. Your time is
    /// logged on that task as Assist hours, and both of you show it on the team board.
    /// </summary>
    public static class Assisting
    {
        public const string Status = "Assist Colleague";

        /// <summary>A task you can help with. A project without sub-categories has no task.</summary>
        public sealed record Option(int ProjectId, int? SubcategoryId, int? TaskId, string Label);

        /// <summary>A clocked-in colleague, what they're on now, and the tasks you can help them with.</summary>
        public sealed record Colleague(string Id, string Name, string Initials, string? PhotoUrl, string Activity,
            List<Option> Options, bool WorkingNow);

        /// <summary>Colleagues who are clocked in now (not admins, not you), people on project work first.</summary>
        public static async Task<List<Colleague>> GetColleaguesAsync(ApplicationDbContext db, string helperId, DateTime now)
        {
            var adminRoleId = await db.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).FirstOrDefaultAsync();
            var adminIds = adminRoleId is null
                ? new List<string>()
                : await db.UserRoles.Where(ur => ur.RoleId == adminRoleId).Select(ur => ur.UserId).ToListAsync();

            var open = await db.AttendanceSessions.AsNoTracking()
                .Where(s => s.ClockOutTime == null && s.EmployeeId != helperId)
                .ToListAsync();
            var clockedIn = open.Where(s => AttendanceRules.GetAutoClockOutTime(s, now) is null)
                .Select(s => s.EmployeeId).Distinct().Where(id => !adminIds.Contains(id)).ToList();
            if (clockedIn.Count == 0) return new();

            var users = await db.Users.AsNoTracking()
                .Where(u => clockedIn.Contains(u.Id) && u.DeactivatedAt == null)
                .ToListAsync();

            // Each person's open tasks (assigned to them, not archived, not with a reviewer or done).
            var assigned = await db.TaskAssignees.AsNoTracking()
                .Where(a => clockedIn.Contains(a.EmployeeId) && a.Task != null && !a.Task.IsArchived
                    && a.Task.Project!.Status != ProjectOptions.StatusCompleted)
                .Select(a => new
                {
                    a.EmployeeId,
                    a.Task!.Id,
                    a.Task.ProjectId,
                    a.Task.SubcategoryId,
                    a.Task.Name,
                    a.Task.Status,
                    Sub = a.Task.Subcategory!.Name,
                    Project = a.Task.Project!.Sample
                })
                .ToListAsync();

            // What each person is working on right now (when it's project work, not checking).
            var currentTaskIds = users.Where(u => u.CurrentCheckId == null && u.CurrentTaskId != null).Select(u => u.CurrentTaskId!.Value).ToList();
            var currentTasks = await db.ProjectTasks.AsNoTracking()
                .Where(t => currentTaskIds.Contains(t.Id))
                .Select(t => new { t.Id, t.ProjectId, t.SubcategoryId, t.Name, t.Status, Sub = t.Subcategory!.Name, Project = t.Project!.Sample })
                .ToDictionaryAsync(t => t.Id);
            var currentProjectIds = users.Where(u => u.CurrentCheckId == null && u.CurrentTaskId == null && u.CurrentProjectId != null)
                .Select(u => u.CurrentProjectId!.Value).ToList();
            var currentProjects = await db.Projects.AsNoTracking()
                .Where(p => currentProjectIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Sample, p.Status, HasSubs = p.Subcategories.Any(s => !s.IsArchived) })
                .ToDictionaryAsync(p => p.Id);

            var result = new List<Colleague>();
            foreach (var u in users)
            {
                var first = string.IsNullOrWhiteSpace(u.FirstName) ? (u.Email ?? "?") : u.FirstName;
                var name = $"{first} {u.LastName}".Trim();
                var initials = (first[..1] + (string.IsNullOrWhiteSpace(u.LastName) ? "" : u.LastName[..1])).ToUpper();

                var options = new List<Option>();
                var working = false;
                string activity = string.IsNullOrWhiteSpace(u.CurrentStatus) ? "Available" : u.CurrentStatus;

                if (u.CurrentCheckId is null && u.CurrentTaskId is int tid && currentTasks.TryGetValue(tid, out var ct))
                {
                    var label = $"{ct.Project} › {ct.Sub} › {ct.Name}";
                    activity = u.CurrentStatus == Status ? $"Assisting: {label}" : $"Working on {label}";
                    if (!ProjectOptions.IsLocked(ct.Status) && u.CurrentStatus != Status)
                    {
                        options.Add(new Option(ct.ProjectId, ct.SubcategoryId, ct.Id, label));
                        working = true;
                    }
                }
                else if (u.CurrentCheckId is null && u.CurrentProjectId is int pid && currentProjects.TryGetValue(pid, out var cp))
                {
                    activity = $"Working on {cp.Sample}";
                    if (!cp.HasSubs && !ProjectOptions.IsLocked(cp.Status) && u.CurrentStatus != Status)
                    {
                        options.Add(new Option(cp.Id, null, null, cp.Sample));
                        working = true;
                    }
                }
                else if (u.CurrentCheckId is not null)
                {
                    activity = u.CurrentStatus ?? "Checking";
                }

                foreach (var a in assigned.Where(a => a.EmployeeId == u.Id && !ProjectOptions.IsLocked(a.Status)).OrderBy(a => a.Project).ThenBy(a => a.Sub).ThenBy(a => a.Name))
                {
                    if (options.Any(o => o.TaskId == a.Id)) continue;
                    options.Add(new Option(a.ProjectId, a.SubcategoryId, a.Id, $"{a.Project} › {a.Sub} › {a.Name}"));
                }

                result.Add(new Colleague(u.Id, name, initials, ProfilePhotos.Url(u.Id, u.PhotoUpdatedAt), activity, options, working));
            }

            return result.OrderByDescending(c => c.WorkingNow).ThenBy(c => c.Name).ToList();
        }

        /// <summary>
        /// Starts assisting: sets the helper's status and logs their time on the colleague's task as
        /// Assist hours. Returns an error message, or null when it worked.
        /// </summary>
        public static async Task<string?> StartAsync(ApplicationDbContext db, string helperId, string colleagueId, Option option, string? note, DateTime now)
        {
            if (helperId == colleagueId) return "Pick someone else to help.";

            // Re-check the task (or project) against the database: it may have gone for checking.
            if (option.TaskId is int taskId)
            {
                var task = await db.ProjectTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId);
                if (task is null || task.IsArchived || ProjectOptions.IsLocked(task.Status))
                    return "That task is being checked or is no longer open. Pick another one.";
            }
            else
            {
                var status = await db.Projects.Where(p => p.Id == option.ProjectId).Select(p => p.Status).FirstOrDefaultAsync();
                if (status is null || ProjectOptions.IsLocked(status)) return "That project is being checked or is closed.";
            }

            var helper = await db.Users.FirstOrDefaultAsync(u => u.Id == helperId);
            if (helper is null) return "Your account couldn't be found.";

            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (note is { Length: > 200 }) note = note[..200];

            helper.CurrentStatus = Status;
            helper.CurrentProjectId = option.ProjectId;
            helper.CurrentSubcategoryId = option.SubcategoryId;
            helper.CurrentTaskId = option.TaskId;
            helper.CurrentCheckId = null;
            helper.AssistingUserId = colleagueId;
            helper.AssistNote = note;
            await db.SaveChangesAsync();

            await ProjectWork.StartAsync(db, helperId, option.ProjectId, option.SubcategoryId, now, ProjectOptions.WorkAssist, option.TaskId, colleagueId);
            return null;
        }
    }
}
