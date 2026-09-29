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

        /// <summary>
        /// Returns the time an open session should be clocked out at, or null if it is still active.
        /// </summary>
        public static DateTime? GetAutoClockOutTime(AttendanceSession session, DateTime now)
        {
            var limit = session.ClockInTime + MaxSessionLength;

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
            foreach (var session in openSessions)
            {
                var autoClockOut = GetAutoClockOutTime(session, now);
                if (autoClockOut is not null)
                {
                    session.ClockOutTime = autoClockOut;
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
                sessionToResume = new AttendanceSession
                {
                    EmployeeId = employeeId,
                    ClockInTime = now
                };
                db.AttendanceSessions.Add(sessionToResume);
            }

            sessionToResume.LastSeenTime = now;
            await db.SaveChangesAsync();
            return sessionToResume;
        }
    }
}
