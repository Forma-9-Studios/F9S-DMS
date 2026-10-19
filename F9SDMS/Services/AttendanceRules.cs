using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    /// <summary>
    /// When an open attendance session should be closed automatically.
    /// </summary>
    public static class AttendanceRules
    {
        /// <summary>A session never counts more than this.</summary>
        public static readonly TimeSpan MaxSessionLength = TimeSpan.FromHours(8);

        /// <summary>How often an open dashboard records that the employee is still there.</summary>
        public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// If the dashboard hasn't been seen for this long (tab closed, laptop asleep, left early),
        /// the session is closed at the last-seen time. Signing back in within this window
        /// resumes the session instead.
        /// </summary>
        public static readonly TimeSpan HeartbeatGracePeriod = TimeSpan.FromMinutes(15);

        /// <summary>How long a regular session may run (null for overtime, which has no limit).</summary>
        public static TimeSpan? GetLimit(AttendanceSession session) =>
            session.IsOvertime ? null : TimeSpan.FromMinutes(session.LimitMinutes ?? (int)MaxSessionLength.TotalMinutes);

        /// <summary>
        /// Returns the time an open session should be clocked out at, or null if it is still active.
        /// </summary>
        public static DateTime? GetAutoClockOutTime(AttendanceSession session, DateTime now)
        {
            // Overtime has no limit of its own; it only ends when the dashboard isn't seen.
            var limit = GetLimit(session) is TimeSpan length ? session.ClockInTime + length : DateTime.MaxValue;

            if (session.LastSeenTime is DateTime lastSeen)
            {
                if (now - lastSeen > HeartbeatGracePeriod)
                {
                    return lastSeen < limit ? lastSeen : limit;
                }
            }

            return now >= limit ? limit : null;
        }

        /// <summary>
        /// Clocks the employee in: closes any of their open sessions that have already ended
        /// (at the time they ended), then resumes the most recent still-active session, or starts
        /// a new one. Saves the changes and returns the session that is now running.
        /// </summary>
        public static async Task<AttendanceSession> ResumeOrStartSessionAsync(
            ApplicationDbContext db, string employeeId, DateTime now)
        {
            var openSessions = await db.AttendanceSessions
                .Where(s => s.EmployeeId == employeeId && s.ClockOutTime == null)
                .OrderByDescending(s => s.ClockInTime)
                .ToListAsync();

            AttendanceSession? sessionToResume = null;
            DateTime? lastEnded = null;
            foreach (var session in openSessions)
            {
                var autoClockOut = GetAutoClockOutTime(session, now);
                if (autoClockOut is not null)
                {
                    session.ClockOutTime = autoClockOut;
                    if (lastEnded is null || autoClockOut > lastEnded)
                    {
                        lastEnded = autoClockOut;
                    }
                }
                else if (sessionToResume is null)
                {
                    // Brief disconnect (closed tab, browser restart): keep the timer running.
                    sessionToResume = session;
                }
                else
                {
                    // Extra duplicates: close now.
                    session.ClockOutTime = now;
                }
            }

            if (sessionToResume is null)
            {
                // Save the sessions closed above first so they count towards today's 8 hours.
                await db.SaveChangesAsync();

                // What's left of today's regular 8 hours. Once it's used up, logging in again
                // starts overtime.
                var regularToday = await GetRegularTimeAsync(db, employeeId, now.Date, now);
                var remaining = MaxSessionLength - regularToday;
                var isOvertime = remaining < TimeSpan.FromMinutes(1);

                sessionToResume = new AttendanceSession
                {
                    EmployeeId = employeeId,
                    ClockInTime = now,
                    IsOvertime = isOvertime,
                    LimitMinutes = isOvertime ? null : (int)Math.Ceiling(remaining.TotalMinutes)
                };
                db.AttendanceSessions.Add(sessionToResume);
            }

            var startingNewSession = sessionToResume.Id == 0;

            sessionToResume.LastSeenTime = now;
            await db.SaveChangesAsync();

            if (startingNewSession)
            {
                // Any project work from the previous session ended when that session did.
                await ProjectWork.StopAsync(db, employeeId, lastEnded ?? now, clearCurrentProject: true);
            }

            return sessionToResume;
        }

        /// <summary>Regular (not overtime) time clocked in sessions that started on <paramref name="date"/>.</summary>
        public static async Task<TimeSpan> GetRegularTimeAsync(ApplicationDbContext db, string employeeId, DateTime date, DateTime now)
        {
            var day = date.Date;
            var next = day.AddDays(1);
            var sessions = await db.AttendanceSessions.AsNoTracking()
                .Where(s => s.EmployeeId == employeeId && !s.IsOvertime && s.ClockInTime >= day && s.ClockInTime < next)
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
    }
}
