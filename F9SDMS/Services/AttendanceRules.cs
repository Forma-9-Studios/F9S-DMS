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
        /// The daily lunch break (3:00 to 4:00 PM). It doesn't count towards the 8 hours, so a
        /// session that spans it ends an hour later (11:00 AM sign-in, 8:00 PM auto logout).
        /// </summary>
        public static readonly TimeSpan LunchStart = new(15, 0, 0);
        public static readonly TimeSpan LunchEnd = new(16, 0, 0);

        /// <summary>How much of [start, end) falls in a lunch break.</summary>
        public static TimeSpan LunchOverlap(DateTime start, DateTime end)
        {
            var total = TimeSpan.Zero;
            for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
            {
                var from = day + LunchStart > start ? day + LunchStart : start;
                var to = day + LunchEnd < end ? day + LunchEnd : end;
                if (to > from) total += to - from;
            }
            return total;
        }

        /// <summary>Time between <paramref name="start"/> and <paramref name="end"/>, not counting lunch.</summary>
        public static TimeSpan WorkTime(DateTime start, DateTime end) =>
            end <= start ? TimeSpan.Zero : end - start - LunchOverlap(start, end);

        /// <summary>When <paramref name="work"/> of working time (lunch excluded) has passed since <paramref name="start"/>.</summary>
        public static DateTime AddWorkTime(DateTime start, TimeSpan work)
        {
            var end = start + work;
            for (var i = 0; i < 4; i++)
            {
                var next = start + work + LunchOverlap(start, end);
                if (next == end) break;
                end = next;
            }
            return end;
        }

        /// <summary>
        /// How much working time (lunch excluded) a regular session may count. Null for overtime,
        /// which has no limit.
        /// </summary>
        public static TimeSpan? GetLimit(AttendanceSession session) =>
            session.IsOvertime ? null : TimeSpan.FromMinutes(session.LimitMinutes ?? (int)MaxSessionLength.TotalMinutes);

        /// <summary>When a regular session is logged out automatically (null for overtime).</summary>
        public static DateTime? GetEndTime(AttendanceSession session) =>
            GetLimit(session) is TimeSpan work ? AddWorkTime(session.ClockInTime, work) : null;

        /// <summary>True during the lunch break.</summary>
        public static bool IsLunch(DateTime now) => now.TimeOfDay >= LunchStart && now.TimeOfDay < LunchEnd;

        /// <summary>
        /// Returns the time an open session should be clocked out at, or null if it is still active.
        /// </summary>
        public static DateTime? GetAutoClockOutTime(AttendanceSession session, DateTime now)
        {
            // Overtime has no limit of its own; it only ends when the dashboard isn't seen.
            var limit = GetEndTime(session) ?? DateTime.MaxValue;

            if (session.LastSeenTime is DateTime lastSeen)
            {
                // Away over lunch (laptop closed at 3:00, back at 4:00) doesn't end the session.
                if (WorkTime(lastSeen, now) > HeartbeatGracePeriod)
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

        /// <summary>Regular (not overtime) time clocked in sessions that started on <paramref name="date"/>, lunch excluded.</summary>
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
                total += WorkTime(s.ClockInTime, s.ClockOutTime ?? now);
            }
            return total;
        }
    }
}
