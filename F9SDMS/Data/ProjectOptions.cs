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
        public const string StatusReturned = "Returned";
        public const string StatusCompleted = "Completed";

        /// <summary>Sub-category (part) status once a checker approves it.</summary>
        public const string StatusApproved = "Approved";

        public static readonly string[] Statuses =
        {
            StatusUnassigned, StatusAssigned, StatusInProgress, StatusForChecking, StatusReturned, StatusCompleted
        };

        /// <summary>Roles a person can have on a project.</summary>
        public const string RoleEngineer = "Engineer";
        public const string RoleChecker = "Checker";

        /// <summary>What a time entry was spent on.</summary>
        public const string WorkModeling = "Modeling";
        public const string WorkChecking = "Checking";

        /// <summary>Results a checker can give a submission.</summary>
        public const string ResultApproved = "Approved";
        public const string ResultReturned = "Returned";

        /// <summary>
        /// A part (sub-category, or a project without sub-categories) that is being checked
        /// or already approved can't be picked as an engineer's status.
        /// </summary>
        public static bool IsLocked(string status) =>
            status == StatusForChecking || status == StatusApproved || status == StatusCompleted;

        /// <summary>A part can be submitted for checking from these statuses.</summary>
        public static bool CanSubmit(string status) =>
            status == StatusAssigned || status == StatusInProgress || status == StatusReturned;

        /// <summary>CSS class suffix for a project status pill.</summary>
        public static string StatusCss(string status) => status switch
        {
            StatusAssigned => "assigned",
            StatusInProgress => "in-progress",
            StatusForChecking => "for-checking",
            StatusReturned => "returned",
            StatusApproved => "completed",
            StatusCompleted => "completed",
            _ => "unassigned"
        };
    }
}
