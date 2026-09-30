namespace F9SDMS.Data
{
    /// <summary>Fixed choices used by the project forms and filters.</summary>
    public static class ProjectOptions
    {
        public static readonly string[] LodOptions =
        {
            "LOD 100", "LOD 200", "LOD 300", "LOD 400", "LOD 500", "Full (LOD 100–500)"
        };

        // Placeholder list; update once the studio confirms the categories.
        public static readonly string[] CategoryOptions =
        {
            "Architectural", "Structural", "MEP", "Fit-out"
        };

        public const string StatusUnassigned = "Unassigned";
        public const string StatusAssigned = "Assigned";
        public const string StatusInProgress = "In Progress";
        public const string StatusForChecking = "For Checking";
        public const string StatusCompleted = "Completed";

        public static readonly string[] Statuses =
        {
            StatusUnassigned, StatusAssigned, StatusInProgress, StatusForChecking, StatusCompleted
        };

        /// <summary>CSS class suffix for a project status pill.</summary>
        public static string StatusCss(string status) => status switch
        {
            StatusAssigned => "assigned",
            StatusInProgress => "in-progress",
            StatusForChecking => "for-checking",
            StatusCompleted => "completed",
            _ => "unassigned"
        };
    }
}
