namespace F9SDMS.Data
{
    /// <summary>Fixed values for IT support tickets.</summary>
    public static class TicketOptions
    {
        public static readonly string[] Categories =
        {
            "Hardware", "Software", "Network / Internet", "Account / Access", "License", "Printer", "Other"
        };

        public const string PriorityLow = "Low";
        public const string PriorityNormal = "Normal";
        public const string PriorityHigh = "High";
        public const string PriorityUrgent = "Urgent";

        public static readonly string[] Priorities = { PriorityLow, PriorityNormal, PriorityHigh, PriorityUrgent };

        /// <summary>Higher number = more urgent (used for sorting the admin queue).</summary>
        public static int PriorityRank(string priority) => priority switch
        {
            PriorityUrgent => 3,
            PriorityHigh => 2,
            PriorityNormal => 1,
            _ => 0
        };

        public const string StatusOpen = "Open";
        public const string StatusInProgress = "In Progress";

        /// <summary>IT replied and is waiting for the employee.</summary>
        public const string StatusWaiting = "Waiting";
        public const string StatusResolved = "Resolved";

        /// <summary>Resolved for more than <see cref="ReopenDays"/> days; can't be reopened.</summary>
        public const string StatusClosed = "Closed";

        public static readonly string[] Statuses = { StatusOpen, StatusInProgress, StatusWaiting, StatusResolved, StatusClosed };

        /// <summary>Tickets still being worked on.</summary>
        public static bool IsActive(string status) => status is StatusOpen or StatusInProgress or StatusWaiting;

        /// <summary>The employee can reply to a resolved ticket (which reopens it) for this many days.</summary>
        public const int ReopenDays = 7;

        public static string Css(string value) => value.ToLowerInvariant().Replace(' ', '-');
    }
}
