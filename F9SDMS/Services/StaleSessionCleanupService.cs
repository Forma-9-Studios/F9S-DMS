using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    public class StaleSessionCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<StaleSessionCleanupService> _logger;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

        public StaleSessionCleanupService(IServiceScopeFactory scopeFactory, ILogger<StaleSessionCleanupService> logger)
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
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var clock = scope.ServiceProvider.GetRequiredService<IAppClock>();
                    var now = clock.Now;

                    var openSessions = await dbContext.AttendanceSessions
                        .Where(s => s.ClockOutTime == null)
                        .ToListAsync(stoppingToken);

                    var staleSessions = new List<AttendanceSession>();
                    foreach (var session in openSessions)
                    {
                        var clockOut = AttendanceRules.GetAutoClockOutTime(session, now);
                        if (clockOut is not null)
                        {
                            session.ClockOutTime = clockOut;
                            staleSessions.Add(session);
                        }
                    }

                    if (staleSessions.Count > 0)
                    {

                        await dbContext.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Auto-closed {Count} stale attendance session(s).", staleSessions.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while checking for stale attendance sessions.");
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }
    }
}