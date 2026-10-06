using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    /// <summary>
    /// Tracks time spent on projects. An employee "works on" a project by choosing it as
    /// their current status; the time entry ends when they pick another status, log out,
    /// or their attendance session ends.
    /// </summary>
    public static class ProjectWork
    {
        /// <summary>
        /// Starts a time entry for the project (closing any other open entry first) and marks
        /// the project In Progress if it was only Assigned. Saves the changes.
        /// </summary>
        public static async Task StartAsync(ApplicationDbContext db, string employeeId, int projectId, int? subcategoryId, DateTime now,
            string workType = ProjectOptions.WorkModeling)
        {
            await CloseOpenEntriesAsync(db, employeeId, now);

            db.ProjectTimeEntries.Add(new ProjectTimeEntry
            {
                ProjectId = projectId,
                SubcategoryId = subcategoryId,
                EmployeeId = employeeId,
                WorkType = workType,
                StartTime = now
            });

            var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project is not null && project.Status == ProjectOptions.StatusAssigned)
            {
                project.Status = ProjectOptions.StatusInProgress;
            }

            await db.SaveChangesAsync();
        }

        /// <summary>
        /// Ends the employee's open time entry at <paramref name="end"/>. When
        /// <paramref name="clearCurrentProject"/> is true (logout, session ended) their status
        /// also goes back to Available if it was a project. Saves the changes.
        /// </summary>
        public static async Task StopAsync(ApplicationDbContext db, string employeeId, DateTime end, bool clearCurrentProject)
        {
            await CloseOpenEntriesAsync(db, employeeId, end);

            if (clearCurrentProject)
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == employeeId);
                if (user is not null && user.CurrentProjectId is not null)
                {
                    user.CurrentProjectId = null;
                    user.CurrentSubcategoryId = null;
                    user.CurrentCheckId = null;
                    user.CurrentStatus = "Available";
                }
            }

            await db.SaveChangesAsync();
        }

        private static async Task CloseOpenEntriesAsync(ApplicationDbContext db, string employeeId, DateTime end)
        {
            var openEntries = await db.ProjectTimeEntries
                .Where(t => t.EmployeeId == employeeId && t.EndTime == null)
                .ToListAsync();

            foreach (var entry in openEntries)
            {
                entry.EndTime = end < entry.StartTime ? entry.StartTime : end;
            }
        }
    }
}
