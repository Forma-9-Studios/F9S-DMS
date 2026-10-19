namespace F9SDMS.Data
{
    /// <summary>Fixed values for overtime and leave requests.</summary>
    public static class RequestOptions
    {
        public const string StatusPending = "Pending";
        public const string StatusApproved = "Approved";
        public const string StatusRejected = "Rejected";
        public const string StatusCancelled = "Cancelled";

        public static readonly string[] Statuses = { StatusPending, StatusApproved, StatusRejected, StatusCancelled };

        /// <summary>Overtime asked for before the shift (date with a from and to time).</summary>
        public const string OvertimePlanned = "Planned";

        /// <summary>Overtime filed after the shift for hours already worked.</summary>
        public const string OvertimeWorked = "Already worked";

        public const string LeaveVacation = "Vacation";
        public const string LeaveSick = "Sick";
        public const string LeaveEmergency = "Emergency";
        public const string LeaveUnpaid = "Unpaid";

        public static readonly string[] LeaveTypes = { LeaveVacation, LeaveSick, LeaveEmergency, LeaveUnpaid };

        /// <summary>Leave types that are taken from the yearly credits.</summary>
        public static bool UsesCredits(string leaveType) => leaveType is LeaveVacation or LeaveSick;

        public const string HalfDayAm = "AM";
        public const string HalfDayPm = "PM";

        /// <summary>CSS-friendly name for a status, e.g. "Pending" -> "pending".</summary>
        public static string StatusCss(string status) => status.ToLowerInvariant();
    }
}
