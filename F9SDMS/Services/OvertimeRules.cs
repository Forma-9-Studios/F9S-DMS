using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    /// <summary>Overtime helpers: extra time allowed on a day, and hours clocked on a day.</summary>
    public static class OvertimeRules
    {
        /// <summary>Approved overtime on <paramref name="date"/>.</summary>
        public static async Task<TimeSpan> GetApprovedExtraAsync(ApplicationDbContext db, string employeeId, DateTime date)
        {
            var day = date.Date;
            var minutes = await db.OvertimeRequests.AsNoTracking()
                .Where(o => o.EmployeeId == employeeId && o.Date == day && o.Status == RequestOptions.StatusApproved)
                .SumAsync(o => (int?)o.Minutes) ?? 0;
            return TimeSpan.FromMinutes(minutes);
        }

        /// <summary>Overtime clocked on <paramref name="date"/> (sessions started after the regular 8 hours).</summary>
        public static async Task<TimeSpan> GetOvertimeClockedAsync(ApplicationDbContext db, string employeeId, DateTime date, DateTime now)
        {
            var day = date.Date;
            var next = day.AddDays(1);
            var sessions = await db.AttendanceSessions.AsNoTracking()
                .Where(s => s.EmployeeId == employeeId && s.IsOvertime && s.ClockInTime >= day && s.ClockInTime < next)
                .Select(s => new { s.ClockInTime, s.ClockOutTime })
                .ToListAsync();

            var total = TimeSpan.Zero;
            foreach (var s in sessions)
            {
                var end = s.ClockOutTime ?? now;
                if (end > s.ClockInTime) total += end - s.ClockInTime;
            }
            return total;
        }

        /// <summary>Total time clocked in on <paramref name="date"/> (sessions that started that day).</summary>
        public static async Task<TimeSpan> GetClockedAsync(ApplicationDbContext db, string employeeId, DateTime date, DateTime now)
        {
            var day = date.Date;
            var next = day.AddDays(1);
            var sessions = await db.AttendanceSessions.AsNoTracking()
                .Where(s => s.EmployeeId == employeeId && s.ClockInTime >= day && s.ClockInTime < next)
                .Select(s => new { s.ClockInTime, s.ClockOutTime })
                .ToListAsync();

            var total = TimeSpan.Zero;
            foreach (var s in sessions)
            {
                var end = s.ClockOutTime ?? now;
                if (end > s.ClockInTime) total += end - s.ClockInTime;
            }
            return total;
        }

        /// <summary>Pending overtime and leave requests waiting for a decision, not counting the viewer's own.</summary>
        public static async Task<int> CountPendingApprovalsAsync(ApplicationDbContext db, string? viewerId)
        {
            var overtime = await db.OvertimeRequests.CountAsync(o => o.Status == RequestOptions.StatusPending && o.EmployeeId != viewerId);
            var leave = await db.LeaveRequests.CountAsync(l => l.Status == RequestOptions.StatusPending && l.EmployeeId != viewerId);
            return overtime + leave;
        }

        /// <summary>Formats minutes as "2h", "1h 30m" or "45m".</summary>
        public static string Format(int minutes)
        {
            var h = minutes / 60;
            var m = minutes % 60;
            if (h > 0 && m > 0) return $"{h}h {m}m";
            return h > 0 ? $"{h}h" : $"{m}m";
        }

        public static string Format(TimeSpan span) => Format((int)Math.Round(Math.Max(0, span.TotalMinutes)));
    }
}
