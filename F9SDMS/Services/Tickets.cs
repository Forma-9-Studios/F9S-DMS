using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    /// <summary>Shared ticket logic: closing old resolved tickets, counts for the menu, notifications.</summary>
    public static class Tickets
    {
        /// <summary>Resolved tickets older than <see cref="TicketOptions.ReopenDays"/> days become Closed.</summary>
        public static async Task AutoCloseAsync(ApplicationDbContext db, DateTime now)
        {
            var cutoff = now.AddDays(-TicketOptions.ReopenDays);
            await db.Tickets
                .Where(t => t.Status == TicketOptions.StatusResolved && t.ResolvedAt != null && t.ResolvedAt < cutoff)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketOptions.StatusClosed));
        }

        /// <summary>
        /// The number on the IT Support menu link: for admins, tickets that need IT (Open or
        /// In Progress); for everyone else, their tickets waiting for their reply.
        /// </summary>
        public static async Task<int> MenuCountAsync(ApplicationDbContext db, string? userId, bool isAdmin)
        {
            if (isAdmin)
            {
                return await db.Tickets.CountAsync(t => t.Status == TicketOptions.StatusOpen || t.Status == TicketOptions.StatusInProgress);
            }
            if (userId is null) return 0;
            return await db.Tickets.CountAsync(t => t.EmployeeId == userId && t.Status == TicketOptions.StatusWaiting);
        }

        /// <summary>Ids of everyone in the Admin role (they handle tickets).</summary>
        public static async Task<List<string>> AdminIdsAsync(ApplicationDbContext db)
        {
            var roleId = await db.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).FirstOrDefaultAsync();
            if (roleId is null) return new();
            return await db.UserRoles.Where(ur => ur.RoleId == roleId).Select(ur => ur.UserId).ToListAsync();
        }

        /// <summary>Adds a bell notification about a ticket (saved with the caller's SaveChanges).</summary>
        public static void Notify(ApplicationDbContext db, string userId, string kind, Ticket ticket, string body, DateTime now)
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Kind = kind,
                Title = $"{ticket.Code} · {ticket.Subject}",
                Body = body.Length > 500 ? body[..497] + "…" : body,
                Link = $"/it-support/{ticket.Id}",
                CreatedAt = now
            });
        }

        /// <summary>"12 min ago", "3 h ago", "Yesterday", "Oct 19".</summary>
        public static string Ago(DateTime when, DateTime now)
        {
            var span = now - when;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (when.Date == now.Date) return $"{(int)span.TotalHours} h ago";
            if (when.Date == now.Date.AddDays(-1)) return "Yesterday";
            return when.ToString("MMM d");
        }

        public static string FormatSize(long bytes) => bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:0.0} MB"
            : $"{Math.Max(1, bytes / 1024)} KB";
    }
}
