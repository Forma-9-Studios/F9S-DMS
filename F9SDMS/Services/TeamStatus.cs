using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    /// <summary>One team member's card on the My team board.</summary>
    public sealed record TeamMember(
        string Id,
        string Name,
        string FirstName,
        string Initials,
        string ManagerId,
        string StateKey,
        string StateLabel,
        string? Context,
        string Headline,
        string? Since,
        TimeSpan Today,
        int OverdueTasks,
        int DueSoonTasks,
        string Footer);

    /// <summary>Today's details for one member (the side panel).</summary>
    public sealed record TeamMemberDetail(
        List<(DateTime Start, DateTime? End, string What, bool Checking)> Timeline,
        TimeSpan Today,
        TimeSpan Week,
        List<(string Title, DateTime? Deadline, string Status, int ProjectId)> Tasks,
        int PendingLeave,
        int PendingOvertime);

    /// <summary>Builds the live team board: what each person is doing right now.</summary>
    public static class TeamStatus
    {
        /// <summary>
        /// Cards for the team of <paramref name="managerId"/>, or for every non-admin employee when
        /// it's null (admins). Sorted: working first, then breaks/meetings, then the rest.
        /// </summary>
        public static async Task<List<TeamMember>> GetAsync(ApplicationDbContext db, string? managerId, DateTime now)
        {
            var today = now.Date;
            var adminRoleId = await db.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).FirstOrDefaultAsync();
            var adminIds = adminRoleId is null
                ? new List<string>()
                : await db.UserRoles.Where(ur => ur.RoleId == adminRoleId).Select(ur => ur.UserId).ToListAsync();

            var usersQuery = db.Users.AsNoTracking().Where(u => !adminIds.Contains(u.Id) && u.DeactivatedAt == null);
            if (managerId is not null)
            {
                usersQuery = usersQuery.Where(u => u.TeamManagerId == managerId);
            }
            var users = await usersQuery.ToListAsync();
            var ids = users.Select(u => u.Id).ToList();
            if (ids.Count == 0) return new();

            var openSessions = await db.AttendanceSessions.AsNoTracking()
                .Where(s => ids.Contains(s.EmployeeId) && s.ClockOutTime == null)
                .ToListAsync();
            var todaySessions = await db.AttendanceSessions.AsNoTracking()
                .Where(s => ids.Contains(s.EmployeeId) && s.ClockInTime >= today)
                .Select(s => new { s.EmployeeId, s.ClockInTime, s.ClockOutTime })
                .ToListAsync();
            var lastSeen = await db.AttendanceSessions.AsNoTracking()
                .Where(s => ids.Contains(s.EmployeeId) && s.ClockOutTime != null)
                .GroupBy(s => s.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Last = g.Max(s => s.ClockOutTime) })
                .ToListAsync();
            var leaveToday = await db.LeaveRequests.AsNoTracking()
                .Where(l => ids.Contains(l.EmployeeId) && l.Status == RequestOptions.StatusApproved && l.StartDate <= today && l.EndDate >= today)
                .ToListAsync();
            var openEntries = await db.ProjectTimeEntries.AsNoTracking()
                .Where(t => ids.Contains(t.EmployeeId) && t.EndTime == null)
                .Select(t => new { t.EmployeeId, t.StartTime })
                .ToListAsync();
            var recentFrom = today.AddDays(-30);
            var lastEntries = (await db.ProjectTimeEntries.AsNoTracking()
                    .Where(t => ids.Contains(t.EmployeeId) && t.TaskId != null && t.StartTime >= recentFrom)
                    .Select(t => new { t.EmployeeId, t.TaskId, t.StartTime })
                    .ToListAsync())
                .GroupBy(t => t.EmployeeId)
                .Select(g => g.OrderByDescending(t => t.StartTime).First())
                .ToList();
            var assigned = await db.TaskAssignees.AsNoTracking()
                .Where(a => ids.Contains(a.EmployeeId) && a.Task != null && !a.Task.IsArchived
                    && a.Task.Status != ProjectOptions.StatusApproved && a.Task.Deadline != null)
                .Select(a => new { a.EmployeeId, Deadline = a.Task!.Deadline!.Value })
                .ToListAsync();

            // Names for the project, sub-category and task each person is on (or last worked on).
            var taskIds = users.Where(u => u.CurrentTaskId != null).Select(u => u.CurrentTaskId!.Value)
                .Concat(lastEntries.Where(e => e.TaskId != null).Select(e => e.TaskId!.Value)).Distinct().ToList();
            var projectIds = users.Where(u => u.CurrentProjectId != null).Select(u => u.CurrentProjectId!.Value).Distinct().ToList();
            var tasks = await db.ProjectTasks.AsNoTracking()
                .Where(t => taskIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name, t.ProjectId, Sub = t.Subcategory!.Name, Project = t.Project!.Sample })
                .ToDictionaryAsync(t => t.Id);
            var projects = await db.Projects.AsNoTracking()
                .Where(p => projectIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Sample })
                .ToDictionaryAsync(p => p.Id);
            var checkIds = users.Where(u => u.CurrentCheckId != null).Select(u => u.CurrentCheckId!.Value).ToList();
            var checks = await db.CheckSubmissions.AsNoTracking()
                .Where(c => checkIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Round })
                .ToDictionaryAsync(c => c.Id);

            var result = new List<TeamMember>();
            foreach (var u in users)
            {
                var first = string.IsNullOrWhiteSpace(u.FirstName) ? (u.Email ?? "?") : u.FirstName;
                var name = $"{first} {u.LastName}".Trim();
                var initials = (first[..1] + (string.IsNullOrWhiteSpace(u.LastName) ? "" : u.LastName[..1])).ToUpper();

                var session = openSessions
                    .Where(s => s.EmployeeId == u.Id && AttendanceRules.GetAutoClockOutTime(s, now) is null)
                    .OrderByDescending(s => s.ClockInTime)
                    .FirstOrDefault();
                var todayTotal = todaySessions.Where(s => s.EmployeeId == u.Id)
                    .Aggregate(TimeSpan.Zero, (sum, s) => sum + ((s.ClockOutTime ?? now) > s.ClockInTime ? (s.ClockOutTime ?? now) - s.ClockInTime : TimeSpan.Zero));
                var overdue = assigned.Count(a => a.EmployeeId == u.Id && a.Deadline.Date < today);
                var dueSoon = assigned.Count(a => a.EmployeeId == u.Id && a.Deadline.Date >= today && a.Deadline.Date <= today.AddDays(1));

                string? TaskTitle(int? taskId, out string? ctx)
                {
                    ctx = null;
                    if (taskId is int tid && tasks.TryGetValue(tid, out var t))
                    {
                        ctx = $"{t.Project} › {t.Sub}";
                        return t.Name;
                    }
                    return null;
                }

                string stateKey, stateLabel, headline, footer;
                string? context = null, since = null;

                if (session is not null)
                {
                    var entry = openEntries.Where(e => e.EmployeeId == u.Id).OrderByDescending(e => e.StartTime).FirstOrDefault();
                    if (entry is not null) since = "for " + Duration(now - entry.StartTime);
                    footer = $"Clocked in {session.ClockInTime:h:mm tt}";

                    if (u.CurrentCheckId is int checkId)
                    {
                        stateKey = "check";
                        stateLabel = session.IsOvertime ? "Overtime · checking" : "Checking";
                        var title = TaskTitle(u.CurrentTaskId, out context) ?? "a submission";
                        headline = $"Checking: {title}" + (checks.TryGetValue(checkId, out var c) ? $" (R{c.Round})" : "");
                    }
                    else if (u.CurrentProjectId is int projectId)
                    {
                        stateKey = session.IsOvertime ? "ot" : "work";
                        stateLabel = session.IsOvertime ? "Overtime" : "Working";
                        var title = TaskTitle(u.CurrentTaskId, out context);
                        headline = title ?? (projects.TryGetValue(projectId, out var p) ? p.Sample : "A project");
                    }
                    else
                    {
                        var status = string.IsNullOrWhiteSpace(u.CurrentStatus) ? "Available" : u.CurrentStatus;
                        stateKey = status switch
                        {
                            "Break" => "break",
                            "Meeting" => "meet",
                            _ => "other"
                        };
                        stateLabel = status;
                        headline = status;
                        var last = lastEntries.FirstOrDefault(e => e.EmployeeId == u.Id);
                        var lastTitle = TaskTitle(last?.TaskId, out var lastContext);
                        if (lastTitle is not null) footer = $"Last task: {lastContext} › {lastTitle}";
                    }
                }
                else if (leaveToday.FirstOrDefault(l => l.EmployeeId == u.Id) is LeaveRequest leave)
                {
                    stateKey = "leave";
                    stateLabel = "On leave";
                    headline = leave.IsHalfDay ? $"{leave.LeaveType} leave ({leave.HalfDayPart})" : $"{leave.LeaveType} leave";
                    since = leave.StartDate == leave.EndDate ? leave.StartDate.ToString("ddd, MMM d") : $"{leave.StartDate:MMM d} – {leave.EndDate:MMM d}";
                    footer = leave.EndDate.Date > today ? $"Back {NextWorkday(leave.EndDate):dddd}" : "";
                }
                else
                {
                    stateKey = "off";
                    stateLabel = "Not clocked in";
                    headline = "Not clocked in";
                    var seen = lastSeen.FirstOrDefault(l => l.EmployeeId == u.Id)?.Last;
                    since = seen is DateTime s
                        ? "last seen " + (s.Date == today ? $"today {s:h:mm tt}" : s.Date == today.AddDays(-1) ? $"yesterday {s:h:mm tt}" : s.ToString("MMM d, h:mm tt"))
                        : "hasn't clocked in yet";
                    footer = "";
                }

                result.Add(new TeamMember(u.Id, name, first, initials, u.TeamManagerId ?? "", stateKey, stateLabel, context, headline, since,
                    todayTotal, overdue, dueSoon, footer));
            }

            int Order(string key) => key switch
            {
                "work" or "check" or "ot" => 0,
                "other" => 1,
                "break" or "meet" => 2,
                "leave" => 3,
                _ => 4
            };
            return result.OrderBy(m => Order(m.StateKey)).ThenBy(m => m.Name).ToList();
        }

        /// <summary>What one person did today, their hours, open tasks and pending requests.</summary>
        public static async Task<TeamMemberDetail> GetDetailAsync(ApplicationDbContext db, string employeeId, DateTime now)
        {
            var today = now.Date;
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));

            var entries = await db.ProjectTimeEntries.AsNoTracking()
                .Where(t => t.EmployeeId == employeeId && t.StartTime >= today)
                .OrderBy(t => t.StartTime)
                .Select(t => new
                {
                    t.StartTime,
                    t.EndTime,
                    t.WorkType,
                    Project = t.Project!.Sample,
                    Sub = t.Subcategory != null ? t.Subcategory.Name : null,
                    Task = t.Task != null ? t.Task.Name : null
                })
                .ToListAsync();
            var timeline = entries.Select(e => (e.StartTime, e.EndTime,
                string.Join(" › ", new[] { e.Project, e.Sub, e.Task }.Where(x => !string.IsNullOrEmpty(x))),
                e.WorkType == ProjectOptions.WorkChecking)).ToList();

            var sessions = await db.AttendanceSessions.AsNoTracking()
                .Where(s => s.EmployeeId == employeeId && s.ClockInTime >= monday)
                .Select(s => new { s.ClockInTime, s.ClockOutTime })
                .ToListAsync();
            var todayTotal = sessions.Where(s => s.ClockInTime >= today)
                .Aggregate(TimeSpan.Zero, (sum, s) => sum + ((s.ClockOutTime ?? now) - s.ClockInTime));
            var weekTotal = sessions
                .Aggregate(TimeSpan.Zero, (sum, s) => sum + ((s.ClockOutTime ?? now) - s.ClockInTime));

            var tasks = await db.TaskAssignees.AsNoTracking()
                .Where(a => a.EmployeeId == employeeId && a.Task != null && !a.Task.IsArchived && a.Task.Status != ProjectOptions.StatusApproved)
                .Select(a => new
                {
                    a.Task!.ProjectId,
                    Project = a.Task.Project!.Sample,
                    Sub = a.Task.Subcategory!.Name,
                    a.Task.Name,
                    a.Task.Deadline,
                    a.Task.Status
                })
                .ToListAsync();

            var pendingLeave = await db.LeaveRequests.CountAsync(l => l.EmployeeId == employeeId && l.Status == RequestOptions.StatusPending);
            var pendingOvertime = await db.OvertimeRequests.CountAsync(o => o.EmployeeId == employeeId && o.Status == RequestOptions.StatusPending);

            return new TeamMemberDetail(
                timeline,
                todayTotal < TimeSpan.Zero ? TimeSpan.Zero : todayTotal,
                weekTotal < TimeSpan.Zero ? TimeSpan.Zero : weekTotal,
                tasks.OrderBy(t => t.Deadline ?? DateTime.MaxValue)
                    .Select(t => ($"{t.Project} › {t.Sub} › {t.Name}", t.Deadline, t.Status, t.ProjectId)).ToList(),
                pendingLeave,
                pendingOvertime);
        }

        public static string Duration(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            var h = (int)span.TotalHours;
            return h > 0 ? $"{h} h {span.Minutes} m" : $"{Math.Max(1, span.Minutes)} m";
        }

        private static DateTime NextWorkday(DateTime date)
        {
            var d = date.Date.AddDays(1);
            while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(1);
            return d;
        }
    }
}
