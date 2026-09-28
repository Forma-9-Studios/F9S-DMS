namespace F9SDMS.Components.Layout
{
    public class DashboardContext
    {
        public bool IsAdmin { get; set; }
        public bool IsManager { get; set; }
        public bool CanViewTeamStatus => IsAdmin || IsManager;
        public string? UserId { get; set; }
        public string EmployeeName { get; set; } = "";
        public string Initials { get; set; } = "";
        public bool IsDarkMode { get; set; }
    }
}