namespace F9SDMS.Services
{
    /// <summary>
    /// The current date and time in the studio's time zone (the "TimeZone" setting in
    /// appsettings.json), regardless of the time zone of the server the app runs on.
    /// Attendance times are recorded in this zone.
    /// </summary>
    public interface IAppClock
    {
        DateTime Now { get; }
        TimeZoneInfo TimeZone { get; }
    }

    public sealed class AppClock : IAppClock
    {
        public AppClock(IConfiguration configuration, ILogger<AppClock> logger)
        {
            var timeZoneId = configuration["TimeZone"];
            TimeZone = TimeZoneInfo.Local;

            if (!string.IsNullOrWhiteSpace(timeZoneId))
            {
                try
                {
                    TimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                }
                catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
                {
                    logger.LogWarning("Time zone '{TimeZone}' was not found; using the server's local time zone instead.", timeZoneId);
                }
            }
        }

        public TimeZoneInfo TimeZone { get; }

        public DateTime Now =>
            DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone), DateTimeKind.Unspecified);
    }
}
