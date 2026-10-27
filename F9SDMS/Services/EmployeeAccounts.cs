using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    /// <summary>
    /// Deactivating, reactivating and deleting employee accounts. Deactivating keeps all history
    /// (hours, requests, checks, tickets); deleting is only allowed for accounts with no history.
    /// </summary>
    public static class EmployeeAccounts
    {
        public const string DeactivatedNote = "Cancelled: account deactivated.";

        /// <summary>
        /// Deactivates the account: ends their open session and project work, cancels pending
        /// requests, removes them from projects, tasks and teams. Saves the changes. The caller
        /// should also update the security stamp so any signed-in browser is logged out.
        /// </summary>
        public static async Task DeactivateAsync(ApplicationDbContext db, string userId, string? adminId, DateTime now)
        {
            await ProjectWork.StopAsync(db, userId, now, clearCurrentProject: true);

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return;

            user.DeactivatedAt = now;
            user.TeamManagerId = null;
            user.CurrentStatus = "Available";
            user.CurrentProjectId = null;
            user.CurrentSubcategoryId = null;
            user.CurrentCheckId = null;
            user.CurrentTaskId = null;

            foreach (var session in await db.AttendanceSessions.Where(s => s.EmployeeId == userId && s.ClockOutTime == null).ToListAsync())
            {
                session.ClockOutTime = now < session.ClockInTime ? session.ClockInTime : now;
            }

            foreach (var request in await db.LeaveRequests.Where(r => r.EmployeeId == userId && r.Status == RequestOptions.StatusPending).ToListAsync())
            {
                request.Status = RequestOptions.StatusCancelled;
                request.DecidedById = adminId;
                request.DecidedAt = now;
                request.DecisionNote = DeactivatedNote;
            }

            foreach (var request in await db.OvertimeRequests.Where(r => r.EmployeeId == userId && r.Status == RequestOptions.StatusPending).ToListAsync())
            {
                request.Status = RequestOptions.StatusCancelled;
                request.DecidedById = adminId;
                request.DecidedAt = now;
                request.DecisionNote = DeactivatedNote;
            }

            db.TaskAssignees.RemoveRange(await db.TaskAssignees.Where(a => a.EmployeeId == userId).ToListAsync());
            db.ProjectAssignments.RemoveRange(await db.ProjectAssignments.Where(a => a.EmployeeId == userId).ToListAsync());

            // If they managed a team, their people move to "No team yet".
            foreach (var member in await db.Users.Where(u => u.TeamManagerId == userId).ToListAsync())
            {
                member.TeamManagerId = null;
            }

            // Unread reminders are no use to anyone now.
            db.Notifications.RemoveRange(await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null).ToListAsync());

            await db.SaveChangesAsync();
        }

        /// <summary>Lets the person sign in again. Projects and team have to be set again.</summary>
        public static async Task ReactivateAsync(ApplicationDbContext db, string userId)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return;
            user.DeactivatedAt = null;
            await db.SaveChangesAsync();
        }

        /// <summary>
        /// True when anything in the app refers to this person (hours, sessions, requests, checks,
        /// tickets, projects they created or manage, decisions they made). Those accounts can only be deactivated.
        /// </summary>
        public static async Task<bool> HasHistoryAsync(ApplicationDbContext db, string userId) =>
            await db.AttendanceSessions.AnyAsync(s => s.EmployeeId == userId)
            || await db.ProjectTimeEntries.AnyAsync(t => t.EmployeeId == userId)
            || await db.LeaveRequests.AnyAsync(r => r.EmployeeId == userId || r.DecidedById == userId)
            || await db.OvertimeRequests.AnyAsync(r => r.EmployeeId == userId || r.DecidedById == userId)
            || await db.LeaveAdjustments.AnyAsync(a => a.EmployeeId == userId || a.CreatedById == userId)
            || await db.CheckSubmissions.AnyAsync(c => c.SubmittedById == userId || c.CheckerId == userId)
            || await db.CheckReviews.AnyAsync(r => r.ReviewerId == userId)
            || await db.Tickets.AnyAsync(t => t.EmployeeId == userId)
            || await db.TicketMessages.AnyAsync(m => m.AuthorId == userId)
            || await db.Projects.AnyAsync(p => p.CreatedById == userId || p.ManagerId == userId);

        /// <summary>
        /// Removes what links to the account (assignments, team membership, notifications) before
        /// it is deleted with UserManager. Only call this when <see cref="HasHistoryAsync"/> is false.
        /// </summary>
        public static async Task RemoveLinksAsync(ApplicationDbContext db, string userId)
        {
            db.TaskAssignees.RemoveRange(await db.TaskAssignees.Where(a => a.EmployeeId == userId).ToListAsync());
            db.ProjectAssignments.RemoveRange(await db.ProjectAssignments.Where(a => a.EmployeeId == userId).ToListAsync());
            db.Notifications.RemoveRange(await db.Notifications.Where(n => n.UserId == userId).ToListAsync());
            foreach (var member in await db.Users.Where(u => u.TeamManagerId == userId).ToListAsync())
            {
                member.TeamManagerId = null;
            }
            await db.SaveChangesAsync();
        }
    }
}
