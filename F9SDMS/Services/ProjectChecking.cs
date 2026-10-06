using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;
using static F9SDMS.Data.ProjectOptions;

namespace F9SDMS.Services
{
    /// <summary>Outcome of a checking action; <see cref="Error"/> is shown to the user.</summary>
    public sealed record CheckResult(bool Ok, string? Error = null)
    {
        public static readonly CheckResult Success = new(true);

        public static CheckResult Fail(string error) => new(false, error);
    }

    /// <summary>A submission waiting in a checker's "To check" list.</summary>
    public sealed record CheckQueueItem(
        int SubmissionId,
        int ProjectId,
        string ProjectCode,
        string Sample,
        int? SubcategoryId,
        string? PartName,
        int Round,
        string SubmittedByName,
        DateTime SubmittedAt,
        string? Note,
        string PdfFileName,
        long PdfSize,
        bool IsStarted)
    {
        public string Title => PartName is null ? $"{ProjectCode} · {Sample}" : $"{ProjectCode} · {Sample} › {PartName}";
    }

    /// <summary>
    /// The checking workflow. A "part" is a sub-category, or the whole project when it has none.
    /// Engineers submit a part with a PDF; it is locked (For Checking) until one of the project's
    /// checkers approves it or returns it with comments. Time spent checking is logged as
    /// Checking hours. When every active sub-category is approved the project is Completed.
    /// </summary>
    public static class ProjectChecking
    {
        public static Task<bool> HasRoleAsync(ApplicationDbContext db, int projectId, string userId, string role) =>
            db.ProjectAssignments.AnyAsync(a => a.ProjectId == projectId && a.EmployeeId == userId && a.Role == role);

        /// <summary>Records a submission and locks the part. The PDF must already be stored.</summary>
        public static async Task<CheckResult> SubmitAsync(ApplicationDbContext db, string employeeId, int projectId, int? subcategoryId,
            string pdfFileName, string pdfStoredName, long pdfSize, string? note, DateTime now)
        {
            if (!await HasRoleAsync(db, projectId, employeeId, RoleEngineer))
            {
                return CheckResult.Fail("Only this project's engineers can submit it for checking.");
            }

            var project = await db.Projects
                .Include(p => p.Subcategories)
                .FirstOrDefaultAsync(p => p.Id == projectId);
            if (project is null)
            {
                return CheckResult.Fail("This project no longer exists.");
            }

            if (!await db.ProjectAssignments.AnyAsync(a => a.ProjectId == projectId && a.Role == RoleChecker))
            {
                return CheckResult.Fail("No checker is assigned to this project yet. Ask your manager to add one.");
            }

            var activeSubs = project.Subcategories.Where(s => !s.IsArchived).ToList();
            ProjectSubcategory? part = null;
            if (subcategoryId is int sid)
            {
                part = activeSubs.FirstOrDefault(s => s.Id == sid);
                if (part is null)
                {
                    return CheckResult.Fail("That sub-category no longer exists.");
                }
                if (!CanSubmit(part.Status))
                {
                    return CheckResult.Fail($"{part.Name} is {part.Status.ToLowerInvariant()}, so it can't be submitted.");
                }
            }
            else
            {
                if (activeSubs.Count > 0)
                {
                    return CheckResult.Fail("Pick the sub-category you're submitting.");
                }
                if (!CanSubmit(project.Status))
                {
                    return CheckResult.Fail($"This project is {project.Status.ToLowerInvariant()}, so it can't be submitted.");
                }
            }

            if (await db.CheckSubmissions.AnyAsync(c => c.ProjectId == projectId && c.SubcategoryId == subcategoryId && c.Result == null))
            {
                return CheckResult.Fail("This is already waiting for checking.");
            }

            var round = await db.CheckSubmissions.CountAsync(c => c.ProjectId == projectId && c.SubcategoryId == subcategoryId) + 1;

            db.CheckSubmissions.Add(new CheckSubmission
            {
                ProjectId = projectId,
                SubcategoryId = subcategoryId,
                Round = round,
                SubmittedById = employeeId,
                SubmittedAt = now,
                PdfFileName = TrimFileName(pdfFileName),
                PdfStoredName = pdfStoredName,
                PdfSize = pdfSize,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
            });

            if (part is not null)
            {
                part.Status = StatusForChecking;
                RecomputeProjectStatus(project);
            }
            else
            {
                project.Status = StatusForChecking;
            }

            await db.SaveChangesAsync();

            // The part is locked now, so anyone modeling it stops.
            var working = await db.Users
                .AsNoTracking()
                .Where(u => u.CurrentProjectId == projectId && u.CurrentSubcategoryId == subcategoryId && u.CurrentCheckId == null)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var userId in working)
            {
                await ProjectWork.StopAsync(db, userId, now, clearCurrentProject: true);
            }

            return CheckResult.Success;
        }

        /// <summary>
        /// The checker picks up a submission: it becomes theirs, their status becomes
        /// "Checking · …" and their time is logged as Checking hours. Requires clock-in (checked by the caller).
        /// </summary>
        public static async Task<CheckResult> StartCheckingAsync(ApplicationDbContext db, string checkerId, int submissionId, DateTime now)
        {
            var submission = await db.CheckSubmissions
                .Include(c => c.Project)
                .Include(c => c.Subcategory)
                .FirstOrDefaultAsync(c => c.Id == submissionId);

            var error = await ValidateCheckerAsync(db, submission, checkerId);
            if (error is not null) return CheckResult.Fail(error);

            submission!.CheckerId = checkerId;
            submission.StartedAt ??= now;

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == checkerId);
            if (user is null)
            {
                return CheckResult.Fail("Your account wasn't found.");
            }

            user.CurrentStatus = "Checking · " + PartTitle(submission.Project!, submission.Subcategory);
            user.CurrentProjectId = submission.ProjectId;
            user.CurrentSubcategoryId = submission.SubcategoryId;
            user.CurrentCheckId = submission.Id;
            await db.SaveChangesAsync();

            await ProjectWork.StartAsync(db, checkerId, submission.ProjectId, submission.SubcategoryId, now, WorkChecking);
            return CheckResult.Success;
        }

        /// <summary>
        /// Approves or returns a submission. Returning needs comments; a marked-up PDF is optional
        /// (it must already be stored). Whoever was checking it goes back to Available.
        /// </summary>
        public static async Task<CheckResult> CompleteAsync(ApplicationDbContext db, string checkerId, int submissionId, bool approve,
            string? comments, string? markupFileName, string? markupStoredName, long? markupSize, DateTime now)
        {
            var submission = await db.CheckSubmissions
                .Include(c => c.Project).ThenInclude(p => p!.Subcategories)
                .Include(c => c.Subcategory)
                .FirstOrDefaultAsync(c => c.Id == submissionId);

            var error = await ValidateCheckerAsync(db, submission, checkerId);
            if (error is not null) return CheckResult.Fail(error);

            comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim();
            if (!approve && comments is null)
            {
                return CheckResult.Fail("Add comments so the engineer knows what to fix.");
            }

            var project = submission!.Project!;
            submission.CheckerId = checkerId;
            submission.StartedAt ??= now;
            submission.Result = approve ? ResultApproved : ResultReturned;
            submission.ResultAt = now;
            submission.Comments = comments;
            if (!string.IsNullOrEmpty(markupStoredName))
            {
                submission.MarkupFileName = TrimFileName(markupFileName ?? "markup.pdf");
                submission.MarkupStoredName = markupStoredName;
                submission.MarkupSize = markupSize;
            }

            if (submission.Subcategory is not null)
            {
                submission.Subcategory.Status = approve ? StatusApproved : StatusReturned;
                RecomputeProjectStatus(project);
            }
            else
            {
                project.Status = approve ? StatusCompleted : StatusReturned;
            }

            await db.SaveChangesAsync();

            var checking = await db.Users
                .AsNoTracking()
                .Where(u => u.CurrentCheckId == submissionId)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var userId in checking)
            {
                await ProjectWork.StopAsync(db, userId, now, clearCurrentProject: true);
            }

            return CheckResult.Success;
        }

        /// <summary>Manager action: an approved part (or completed project without sub-categories) goes back to In Progress.</summary>
        public static async Task<CheckResult> ReopenAsync(ApplicationDbContext db, int projectId, int? subcategoryId)
        {
            var project = await db.Projects
                .Include(p => p.Subcategories)
                .FirstOrDefaultAsync(p => p.Id == projectId);
            if (project is null)
            {
                return CheckResult.Fail("This project no longer exists.");
            }

            if (subcategoryId is int sid)
            {
                var part = project.Subcategories.FirstOrDefault(s => s.Id == sid);
                if (part is null || part.Status != StatusApproved)
                {
                    return CheckResult.Fail("Only approved sub-categories can be reopened.");
                }
                part.Status = StatusInProgress;
                RecomputeProjectStatus(project);
            }
            else
            {
                if (project.Status != StatusCompleted)
                {
                    return CheckResult.Fail("Only completed projects can be reopened.");
                }
                project.Status = StatusInProgress;
            }

            await db.SaveChangesAsync();
            return CheckResult.Success;
        }

        /// <summary>Submissions waiting for this checker: unclaimed ones on their projects and ones they started.</summary>
        public static async Task<List<CheckQueueItem>> GetQueueAsync(ApplicationDbContext db, string checkerId)
        {
            var projectIds = await db.ProjectAssignments
                .AsNoTracking()
                .Where(a => a.EmployeeId == checkerId && a.Role == RoleChecker)
                .Select(a => a.ProjectId)
                .ToListAsync();
            if (projectIds.Count == 0) return new();

            var open = await db.CheckSubmissions
                .AsNoTracking()
                .Include(c => c.Project)
                .Include(c => c.Subcategory)
                .Where(c => projectIds.Contains(c.ProjectId) && c.Result == null && (c.CheckerId == null || c.CheckerId == checkerId))
                .OrderBy(c => c.SubmittedAt)
                .ToListAsync();
            if (open.Count == 0) return new();

            var names = await GetNamesAsync(db);
            return open.Select(c => new CheckQueueItem(
                c.Id,
                c.ProjectId,
                c.Project!.Code,
                c.Project.Sample,
                c.SubcategoryId,
                c.Subcategory?.Name,
                c.Round,
                names.TryGetValue(c.SubmittedById, out var n) ? n : "Unknown",
                c.SubmittedAt,
                c.Note,
                c.PdfFileName,
                c.PdfSize,
                c.CheckerId == checkerId)).ToList();
        }

        /// <summary>Display names of every user, by id.</summary>
        public static async Task<Dictionary<string, string>> GetNamesAsync(ApplicationDbContext db)
        {
            var users = await db.Users
                .AsNoTracking()
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToListAsync();
            return users.ToDictionary(u => u.Id, u =>
            {
                var name = $"{u.FirstName} {u.LastName}".Trim();
                return string.IsNullOrEmpty(name) ? (u.Email ?? "Unknown") : name;
            });
        }

        /// <summary>
        /// For projects with sub-categories: Completed when every active part is approved,
        /// For Checking when the rest are all waiting on checkers, otherwise In Progress.
        /// </summary>
        public static void RecomputeProjectStatus(Project project)
        {
            var active = project.Subcategories.Where(s => !s.IsArchived).ToList();
            if (active.Count == 0) return;

            if (active.All(s => s.Status == StatusApproved))
            {
                project.Status = StatusCompleted;
            }
            else if (active.All(s => s.Status == StatusApproved || s.Status == StatusForChecking))
            {
                project.Status = StatusForChecking;
            }
            else
            {
                project.Status = StatusInProgress;
            }
        }

        public static string PartTitle(Project project, ProjectSubcategory? part) =>
            part is null ? $"{project.Code} · {project.Sample}" : $"{project.Code} · {project.Sample} › {part.Name}";

        private static async Task<string?> ValidateCheckerAsync(ApplicationDbContext db, CheckSubmission? submission, string checkerId)
        {
            if (submission?.Project is null) return "This submission no longer exists.";
            if (!submission.IsOpen) return "This was already checked.";
            if (!await HasRoleAsync(db, submission.ProjectId, checkerId, RoleChecker)) return "You're not a checker on this project.";
            if (submission.CheckerId is not null && submission.CheckerId != checkerId) return "Another checker is already checking this.";
            return null;
        }

        private static string TrimFileName(string name)
        {
            name = Path.GetFileName(name.Trim());
            if (string.IsNullOrEmpty(name)) name = "file.pdf";
            return name.Length <= 260 ? name : name[..200] + ".pdf";
        }
    }
}
