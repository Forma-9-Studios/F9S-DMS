using Microsoft.AspNetCore.Identity;

namespace F9SDMS.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = "Available";

        /// <summary>The project the employee is working on right now (their status), if any.</summary>
        public int? CurrentProjectId { get; set; }

        /// <summary>The sub-category of <see cref="CurrentProjectId"/> being worked on, if any.</summary>
        public int? CurrentSubcategoryId { get; set; }

        /// <summary>The check submission this person is checking right now, if any.</summary>
        public int? CurrentCheckId { get; set; }

        /// <summary>The task of <see cref="CurrentSubcategoryId"/> being worked on (or checked), if any.</summary>
        public int? CurrentTaskId { get; set; }

        /// <summary>
        /// First day of work. Vacation and sick leave are earned from this date
        /// (from January 1 when it isn't set).
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Set when the admin creates the account or resets the password: the person must choose
        /// their own password before using the app.
        /// </summary>
        public bool MustChangePassword { get; set; }

        /// <summary>The manager whose team this person is on (their My team tab), if any.</summary>
        public string? TeamManagerId { get; set; }

        /// <summary>
        /// When the admin deactivated the account (null = active). Deactivated people can't sign in
        /// and are hidden from teams and pickers, but their history stays.
        /// </summary>
        public DateTime? DeactivatedAt { get; set; }
    }
}