using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    /// <summary>One leave type's balance for one employee and year.</summary>
    public sealed record LeaveBalance(double Earned, double Carried, double Adjusted, double Used, double Pending)
    {
        /// <summary>Days that can still be requested.</summary>
        public double Available => Math.Round(Earned + Carried + Adjusted - Used - Pending, 2);

        /// <summary>Days the employee has in total this year (earned, carried over and adjustments).</summary>
        public double Total => Math.Round(Earned + Carried + Adjusted, 2);
    }

    /// <summary>Vacation and sick balances for one employee.</summary>
    public sealed record EmployeeLeave(string EmployeeId, LeaveBalance Vacation, LeaveBalance Sick)
    {
        public LeaveBalance For(string leaveType) => leaveType == RequestOptions.LeaveSick ? Sick : Vacation;
    }

    /// <summary>
    /// Leave credits: Vacation and Sick leave are earned evenly each month (yearly days / 12)
    /// at the end of every full month worked, from the employee's start date (or January 1).
    /// Unused vacation moves to the next year up to the policy's carry-over limit.
    /// </summary>
    public static class LeaveRules
    {
        public static async Task<LeavePolicy> GetPolicyAsync(ApplicationDbContext db)
        {
            var policy = await db.LeavePolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == 1);
            return policy ?? new LeavePolicy { Id = 1 };
        }

        /// <summary>Working days (Monday to Friday) from <paramref name="start"/> to <paramref name="end"/>, both included.</summary>
        public static int CountWorkingDays(DateTime start, DateTime end)
        {
            var count = 0;
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
            {
                if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Full months of <paramref name="year"/> that have ended by <paramref name="asOf"/> and that
        /// the employee worked from the first day (a person who starts mid-month earns from the next month).
        /// </summary>
        public static int MonthsEarned(int year, DateTime asOf, DateTime? startDate)
        {
            var months = 0;
            for (var m = 1; m <= 12; m++)
            {
                var monthStart = new DateTime(year, m, 1);
                var monthEnd = monthStart.AddMonths(1); // the month counts once this day is reached
                if (asOf.Date < monthEnd) break;
                if (startDate is DateTime s && s.Date > monthStart) continue;
                months++;
            }
            return months;
        }

        /// <summary>Days earned in <paramref name="year"/> for a yearly allowance, rounded to 2 decimals.</summary>
        public static double Earned(double daysPerYear, int year, DateTime asOf, DateTime? startDate) =>
            Math.Round(daysPerYear / 12.0 * MonthsEarned(year, asOf, startDate), 2);

        /// <summary>Formats a number of days without trailing zeros: 8.25, 6, 0.5.</summary>
        public static string Days(double days) => Math.Round(days, 2).ToString("0.##");

        /// <summary>
        /// Vacation and sick balances for <paramref name="year"/> as of <paramref name="asOf"/>, for
        /// each of the given employees. Requests are counted in the year they start.
        /// </summary>
        public static async Task<Dictionary<string, EmployeeLeave>> GetBalancesAsync(
            ApplicationDbContext db, IReadOnlyCollection<ApplicationUser> employees, int year, DateTime asOf, LeavePolicy? policy = null)
        {
            policy ??= await GetPolicyAsync(db);
            var ids = employees.Select(e => e.Id).ToList();

            // Everything from the earliest start year is needed to work out carry-over.
            var firstYear = Math.Min(year, employees.Select(e => e.StartDate?.Year ?? year).DefaultIfEmpty(year).Min());
            var from = new DateTime(firstYear, 1, 1);
            var to = new DateTime(year + 1, 1, 1);

            var requests = await db.LeaveRequests.AsNoTracking()
                .Where(l => ids.Contains(l.EmployeeId) && l.StartDate >= from && l.StartDate < to
                    && (l.Status == RequestOptions.StatusApproved || l.Status == RequestOptions.StatusPending)
                    && l.CreditDays > 0)
                .Select(l => new { l.EmployeeId, l.LeaveType, l.StartDate, l.Status, l.CreditDays })
                .ToListAsync();

            var adjustments = await db.LeaveAdjustments.AsNoTracking()
                .Where(a => ids.Contains(a.EmployeeId) && a.Year >= firstYear && a.Year <= year)
                .Select(a => new { a.EmployeeId, a.LeaveType, a.Year, a.Days })
                .ToListAsync();

            var result = new Dictionary<string, EmployeeLeave>();
            foreach (var emp in employees)
            {
                LeaveBalance Build(string type, double perYear, int y, double carried, DateTime asOfDate)
                {
                    var used = requests.Where(r => r.EmployeeId == emp.Id && r.LeaveType == type && r.StartDate.Year == y && r.Status == RequestOptions.StatusApproved).Sum(r => r.CreditDays);
                    var pending = requests.Where(r => r.EmployeeId == emp.Id && r.LeaveType == type && r.StartDate.Year == y && r.Status == RequestOptions.StatusPending).Sum(r => r.CreditDays);
                    var adjusted = adjustments.Where(a => a.EmployeeId == emp.Id && a.LeaveType == type && a.Year == y).Sum(a => a.Days);
                    return new LeaveBalance(Earned(perYear, y, asOfDate, emp.StartDate), carried, Math.Round(adjusted, 2), Math.Round(used, 2), Math.Round(pending, 2));
                }

                // Vacation: carry unused days forward year by year (only full past years).
                var startYear = Math.Min(year, emp.StartDate?.Year ?? year);
                double carried = 0;
                for (var y = startYear; y < year; y++)
                {
                    var past = Build(RequestOptions.LeaveVacation, policy.VacationDaysPerYear, y, carried, new DateTime(y + 1, 1, 1));
                    carried = Math.Round(Math.Clamp(past.Available, 0, Math.Max(0, policy.CarryOverMaxDays)), 2);
                }

                var vacation = Build(RequestOptions.LeaveVacation, policy.VacationDaysPerYear, year, carried, asOf);
                var sick = Build(RequestOptions.LeaveSick, policy.SickDaysPerYear, year, 0, asOf);
                result[emp.Id] = new EmployeeLeave(emp.Id, vacation, sick);
            }

            return result;
        }

        /// <summary>
        /// Splits a request's days into credit days and unpaid days, given what is available.
        /// Emergency leave uses no credits; Unpaid leave is all unpaid.
        /// </summary>
        public static (double Credit, double Unpaid) Split(string leaveType, double totalDays, double available)
        {
            if (leaveType == RequestOptions.LeaveUnpaid) return (0, totalDays);
            if (!RequestOptions.UsesCredits(leaveType)) return (0, 0);

            var credit = Math.Round(Math.Clamp(available, 0, totalDays), 2);
            // Credits are used in half-day steps so a request never ends on an odd fraction.
            credit = Math.Floor(credit * 2) / 2;
            return (credit, Math.Round(totalDays - credit, 2));
        }
    }
}
