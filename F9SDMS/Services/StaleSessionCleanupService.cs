using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    public class StaleSessionCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<StaleSessionCleanupService> _logger;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan MaxSessionLength = TimeSpan.FromHours(8);

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

                    var cutoff = DateTime.Now - MaxSessionLength;

                    var staleSessions = await dbContext.AttendanceSessions
                        .Where(s => s.ClockOutTime == null && s.ClockInTime <= cutoff)
                        .ToListAsync(stoppingToken);

                    if (staleSessions.Count > 0)
                    {
                        foreach (var session in staleSessions)
                        {
                            session.ClockOutTime = session.ClockInTime.AddHours(8);
                        }

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