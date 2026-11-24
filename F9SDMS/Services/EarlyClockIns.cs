using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    /// <summary>
    /// Early clock-ins: signing in before 10:45 AM. The person's manager (or every manager when they
    /// have no team) gets a notification and approves or rejects the early time. Approved: the day
    /// counts from the sign-in. Rejected or still pending: it counts from 11:00 AM.
    /// </summary>
    public static class EarlyClockIns
    {
        /// <summary>A pending or decided early clock-in, for the Approvals page and the bell.</summary>
        public sealed record Item(int SessionId, string EmployeeId, string Name, string? PhotoUrl, string Initials,
            DateTime ClockIn, TimeSpan EarlyBy, string Status, string? DecidedBy, DateTime? DecidedAt);

        /// <summary>The bell notification's link; the session id is read back from it.</summary>
        public static string LinkFor(int sessionId) => $"/approvals?early={sessionId}";

        public static int? SessionIdFrom(string? link)
        {
            const string marker = "?early=";
            if (string.IsNullOrEmpty(link)) return null;
            var at = link.IndexOf(marker, StringComparison.Ordinal);
            return at >= 0 && int.TryParse(link[(at + marker.Length)..], out var id) ? id : null;
        }

        public static TimeSpan EarlyBy(DateTime clockIn) => clockIn.Date + AttendanceRules.WorkStart - clockIn;

        /// <summary>"1 h 18 m" / "40 m".</summary>
        public static string Format(TimeSpan span)
        {
            var minutes = Math.Max(0, (int)Math.Round(span.TotalMinutes));
            return minutes >= 60 ? $"{minutes / 60} h {minutes % 60:D2} m" : $"{minutes} m";
        }

        /// <summary>Tells the employee's team manager (or all managers if they have none) about an early clock-in.</summary>
        public static async Task NotifyManagersAsync(ApplicationDbContext db, AttendanceSession session, DateTime now)
        {
            var employee = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == session.EmployeeId);
            if (employee is null) return;

            var managerRoleId = await db.Roles.Where(r => r.Name == "Manager").Select(r => r.Id).FirstOrDefaultAsync();
            var managerIds = managerRoleId is null
                ? new List<string>()
                : await db.UserRoles.Where(ur => ur.RoleId == managerRoleId)
                    .Join(db.Users.Where(u => u.DeactivatedAt == null), ur => ur.UserId, u => u.Id, (ur, u) => ur.UserId)
                    .ToListAsync();

            var recipients = employee.TeamManagerId is string teamManager && managerIds.Contains(teamManager)
                ? new List<string> { teamManager }
                : managerIds.Where(id => id != employee.Id).ToList();
            if (recipients.Count == 0)
            {
                // No managers: admins decide.
                recipients = await Tickets.AdminIdsAsync(db);
            }

            var name = $"{employee.FirstName} {employee.LastName}".Trim();
            foreach (var userId in recipients.Distinct())
            {
                db.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Kind = Notification.KindEarlyClockIn,
                    Title = $"{name} clocked in early at {session.ClockInTime:h:mm tt}",
                    Body = $"{Format(EarlyBy(session.ClockInTime))} before 11:00 AM. Approve to count the early time.",
                    Link = LinkFor(session.Id),
                    CreatedAt = now
                });
            }
            await db.SaveChangesAsync();
        }

        /// <summary>
        /// Approves or rejects a pending early clock-in. Returns false if it was already decided
        /// (or doesn't exist). Notifies the employee and clears the managers' notifications.
        /// </summary>
        public static async Task<bool> DecideAsync(ApplicationDbContext db, int sessionId, bool approve, string deciderId, DateTime now)
        {
            var session = await db.AttendanceSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session is null || session.EarlyStatus != RequestOptions.StatusPending) return false;

            session.EarlyStatus = approve ? RequestOptions.StatusApproved : RequestOptions.StatusRejected;
            session.EarlyDecidedById = deciderId;
            session.EarlyDecidedAt = now;
            if (approve)
            {
                // The day now counts from the actual sign-in (and the automatic logout moves earlier).
                session.CountFrom = null;
            }

            var link = LinkFor(sessionId);
            foreach (var n in await db.Notifications.Where(n => n.Kind == Notification.KindEarlyClockIn && n.Link == link && n.ReadAt == null).ToListAsync())
            {
                n.ReadAt = now;
            }

            var decider = await db.Users.AsNoTracking().Where(u => u.Id == deciderId).Select(u => u.FirstName).FirstOrDefaultAsync() ?? "Your manager";
            db.Notifications.Add(new Notification
            {
                UserId = session.EmployeeId,
                Kind = approve ? Notification.KindRequestApproved : Notification.KindRequestRejected,
                Title = approve ? "Early clock-in approved" : "Early clock-in not approved",
                Body = approve
                    ? $"{decider} approved your early clock-in at {session.ClockInTime:h:mm tt}. Your day counts from then."
                    : $"{decider} didn't approve your early clock-in at {session.ClockInTime:h:mm tt}. Your day counts from 11:00 AM.",
                Link = "/",
                CreatedAt = now
            });

            await db.SaveChangesAsync();
            return true;
        }

        /// <summary>Early clock-ins waiting for a decision.</summary>
        public static Task<int> CountPendingAsync(ApplicationDbContext db, string? viewerId) =>
            db.AttendanceSessions.CountAsync(s => s.EarlyStatus == RequestOptions.StatusPending && s.EmployeeId != viewerId);

        /// <summary>Pending early clock-ins plus those decided in the last 7 days, newest first.</summary>
        public static async Task<List<Item>> ListAsync(ApplicationDbContext db, DateTime now)
        {
            var since = now.Date.AddDays(-7);
            var sessions = await db.AttendanceSessions.AsNoTracking()
                .Where(s => s.EarlyStatus == RequestOptions.StatusPending || (s.EarlyStatus != null && s.ClockInTime >= since))
                .OrderByDescending(s => s.ClockInTime)
                .ToListAsync();
            var ids = sessions.Select(s => s.EmployeeId).Concat(sessions.Where(s => s.EarlyDecidedById != null).Select(s => s.EarlyDecidedById!)).Distinct().ToList();
            var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email, u.PhotoUpdatedAt })
                .ToDictionaryAsync(u => u.Id);

            return sessions.Select(s =>
            {
                users.TryGetValue(s.EmployeeId, out var u);
                var first = u is null ? "Former employee" : string.IsNullOrWhiteSpace(u.FirstName) ? (u.Email ?? "?") : u.FirstName;
                var name = u is null ? first : $"{first} {u.LastName}".Trim();
                var initials = (first[..1] + (string.IsNullOrWhiteSpace(u?.LastName) ? "" : u!.LastName[..1])).ToUpper();
                var decidedBy = s.EarlyDecidedById is string d && users.TryGetValue(d, out var du) ? du.FirstName : null;
                return new Item(s.Id, s.EmployeeId, name, ProfilePhotos.Url(s.EmployeeId, u?.PhotoUpdatedAt), initials,
                    s.ClockInTime, EarlyBy(s.ClockInTime), s.EarlyStatus!, decidedBy, s.EarlyDecidedAt);
            }).ToList();
        }
    }
}
