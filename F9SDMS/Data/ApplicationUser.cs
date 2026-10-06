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
    }
}