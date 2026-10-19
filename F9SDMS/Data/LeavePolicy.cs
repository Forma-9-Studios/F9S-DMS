using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>The company's leave rules (a single row, Id 1). Admins change it on the Approvals page.</summary>
    public class LeavePolicy
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Vacation days earned per calendar year (earned evenly each month).</summary>
        public double VacationDaysPerYear { get; set; } = 15;

        /// <summary>Sick days earned per calendar year (earned evenly each month).</summary>
        public double SickDaysPerYear { get; set; } = 10;

        /// <summary>Vacation must start at least this many days after it is filed.</summary>
        public int VacationNoticeDays { get; set; } = 3;

        /// <summary>Unused vacation days that move to the next year (0 = none).</summary>
        public double CarryOverMaxDays { get; set; } = 5;
    }
}
