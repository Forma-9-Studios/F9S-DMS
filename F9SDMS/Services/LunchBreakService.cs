using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    /// <summary>
    /// At the start of lunch (3:00 PM) everyone who is clocked in is set to "Lunch Break" and any
    /// project work is paused; at the end of lunch (4:00 PM) whoever is still on "Lunch Break" goes
    /// back to "Available". People can still change their status during lunch if they keep working.
    /// </summary>
    public class LunchBreakService : BackgroundService
    {
        public const string LunchStatus = "Lunch Break";
        private const string AvailableStatus = "Available";
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LunchBreakService> _logger;

        // Each switch happens once a day, so changing status during lunch isn't undone a minute later.
        private DateTime? _startedOn;
        private DateTime? _endedOn;

        public LunchBreakService(IServiceScopeFactory scopeFactory, ILogger<LunchBreakService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var clock = scope.ServiceProvider.GetRequiredService<IAppClock>();
                    var now = clock.Now;
                    var today = now.Date;

                    if (AttendanceRules.IsLunch(now) && _startedOn != today)
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var count = await StartLunchAsync(db, now);
                        _startedOn = today;
                        if (count > 0) _logger.LogInformation("Lunch break: set {Count} people to {Status}.", count, LunchStatus);
                    }
                    else if (now.TimeOfDay >= AttendanceRules.LunchEnd && _endedOn != today)
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var count = await EndLunchAsync(db);
                        _endedOn = today;
                        if (count > 0) _logger.LogInformation("Lunch over: set {Count} people back to {Status}.", count, AvailableStatus);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while applying the lunch break status.");
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }

        /// <summary>Pauses project work and sets "Lunch Break" for everyone clocked in.</summary>
        private static async Task<int> StartLunchAsync(ApplicationDbContext db, DateTime now)
        {
            var open = await db.AttendanceSessions
                .Where(s => s.ClockOutTime == null)
                .ToListAsync();
            var clockedIn = open
                .Where(s => AttendanceRules.GetAutoClockOutTime(s, now) is null)
                .Select(s => s.EmployeeId)
                .Distinct()
                .ToList();

            foreach (var employeeId in clockedIn)
            {
                // Ends the open project time entry so lunch isn't logged as project hours.
                await ProjectWork.StopAsync(db, employeeId, now, clearCurrentProject: true);

                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == employeeId);
                if (user is null) continue;
                user.CurrentStatus = LunchStatus;
                user.CurrentProjectId = null;
                user.CurrentSubcategoryId = null;
                user.CurrentCheckId = null;
                user.CurrentTaskId = null;
            }

            await db.SaveChangesAsync();
            return clockedIn.Count;
        }

        /// <summary>Sets everyone still on "Lunch Break" back to "Available".</summary>
        private static async Task<int> EndLunchAsync(ApplicationDbContext db)
        {
            var onLunch = await db.Users.Where(u => u.CurrentStatus == LunchStatus).ToListAsync();
            foreach (var user in onLunch)
            {
                user.CurrentStatus = AvailableStatus;
            }
            await db.SaveChangesAsync();
            return onLunch.Count;
        }
    }
}
