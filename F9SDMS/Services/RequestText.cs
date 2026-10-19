using F9SDMS.Data;

namespace F9SDMS.Services
{
    /// <summary>How overtime and leave requests are described on the Requests and Approvals pages.</summary>
    public static class RequestText
    {
        public static string OvertimeDetails(OvertimeRequest o)
        {
            var length = OvertimeRules.Format(o.Minutes);
            if (o.Kind == RequestOptions.OvertimePlanned && o.StartTime is TimeSpan s && o.EndTime is TimeSpan e)
            {
                return $"Planned · {DateTime.Today.Add(s):h:mm tt} – {DateTime.Today.Add(e):h:mm tt} · {length}";
            }
            return $"Already worked · {length}";
        }

        public static string LeaveDateText(LeaveRequest l) =>
            l.IsHalfDay || l.StartDate == l.EndDate
                ? l.StartDate.ToString("ddd, MMM d")
                : $"{l.StartDate:MMM d} – {l.EndDate:MMM d}";

        public static string LeaveDetails(LeaveRequest l)
        {
            var length = l.IsHalfDay
                ? $"Half day ({l.HalfDayPart})"
                : $"{LeaveRules.Days(l.TotalDays)} {(l.TotalDays == 1 ? "day" : "days")}";
            var text = $"{l.LeaveType} · {length}";
            if (l.LeaveType != RequestOptions.LeaveUnpaid && l.UnpaidDays > 0)
            {
                text += $" ({LeaveRules.Days(l.UnpaidDays)} unpaid)";
            }
            return text;
        }
    }
}
