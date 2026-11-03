using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    /// <summary>
    /// Permanently deletes a project and everything attached to it: sub-categories, tasks and their
    /// assignees, logged hours, check submissions and reviews (and their PDF files), project
    /// assignments and related notifications. Overtime requests that mention the project are kept
    /// (they just no longer point to it). Meant for removing test projects; it can't be undone.
    /// </summary>
    public static class ProjectDeletion
    {
        /// <summary>What was removed, for the confirmation message.</summary>
        public sealed record Result(string Code, string Sample, int Tasks, int TimeEntries, int Checks);

        public static async Task<Result?> DeleteAsync(ApplicationDbContext db, CheckFileStore files, int projectId, DateTime now)
        {
            var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project is null) return null;

            var taskIds = await db.ProjectTasks.Where(t => t.ProjectId == projectId).Select(t => t.Id).ToListAsync();
            var submissions = await db.CheckSubmissions.Include(c => c.Reviews).Where(c => c.ProjectId == projectId).ToListAsync();
            var submissionIds = submissions.Select(c => c.Id).ToList();

            // Anyone working on or checking this project stops now (their status goes back to Available).
            var working = await db.Users
                .Where(u => u.CurrentProjectId == projectId || (u.CurrentCheckId != null && submissionIds.Contains(u.CurrentCheckId.Value)))
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var userId in working)
            {
                await ProjectWork.StopAsync(db, userId, now, clearCurrentProject: true);
                var user = await db.Users.FirstAsync(u => u.Id == userId);
                user.CurrentProjectId = null;
                user.CurrentSubcategoryId = null;
                user.CurrentCheckId = null;
                user.CurrentTaskId = null;
                user.CurrentStatus = "Available";
            }

            // Files to remove once the database rows are gone.
            var storedFiles = submissions.SelectMany(c => new[] { c.PdfStoredName, c.MarkupStoredName })
                .Concat(submissions.SelectMany(c => c.Reviews).Select(r => r.FileStoredName))
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            var timeEntries = await db.ProjectTimeEntries.Where(t => t.ProjectId == projectId).ToListAsync();

            db.Notifications.RemoveRange(await db.Notifications
                .Where(n => n.ProjectId == projectId || (n.TaskId != null && taskIds.Contains(n.TaskId.Value)))
                .ToListAsync());
            db.CheckReviews.RemoveRange(submissions.SelectMany(c => c.Reviews));
            db.CheckSubmissions.RemoveRange(submissions);
            db.ProjectTimeEntries.RemoveRange(timeEntries);
            db.TaskAssignees.RemoveRange(await db.TaskAssignees.Where(a => taskIds.Contains(a.TaskId)).ToListAsync());
            db.ProjectTasks.RemoveRange(await db.ProjectTasks.Where(t => t.ProjectId == projectId).ToListAsync());
            db.ProjectSubcategories.RemoveRange(await db.ProjectSubcategories.Where(s => s.ProjectId == projectId).ToListAsync());
            db.ProjectAssignments.RemoveRange(await db.ProjectAssignments.Where(a => a.ProjectId == projectId).ToListAsync());

            foreach (var request in await db.OvertimeRequests.Where(o => o.ProjectId == projectId).ToListAsync())
            {
                request.ProjectId = null;
            }

            db.Projects.Remove(project);

            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            foreach (var name in storedFiles)
            {
                files.Delete(name);
            }

            return new Result(project.Code, project.Sample, taskIds.Count, timeEntries.Count, submissions.Count);
        }
    }
}
