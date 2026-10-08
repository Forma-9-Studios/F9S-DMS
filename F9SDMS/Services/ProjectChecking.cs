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

    /// <summary>A submission waiting for this person, as a checker or as the project manager.</summary>
    public sealed record CheckQueueItem(
        int SubmissionId,
        int ProjectId,
        string ProjectCode,
        string Sample,
        string? PartName,
        string? TaskName,
        int Round,
        string Level,
        string SubmittedByName,
        DateTime SubmittedAt,
        string? EngineerNote,
        string? CheckerName,
        string? CheckerNote,
        string? ManagerComments,
        string PdfUrl,
        string PdfFileName,
        long PdfSize,
        string? MarkupUrl,
        string? MarkupFileName,
        bool IsStarted,
        bool ReturnedByManager)
    {
        public bool IsManagerLevel => Level == StageManager;

        public string Title => ProjectChecking.FormatTitle(ProjectCode, Sample, PartName, TaskName);

        /// <summary>The part without the project code, e.g. "Tabuk › FEC › Lighting layout".</summary>
        public string ShortTitle => string.Join(" › ", new[] { Sample, PartName, TaskName }.Where(x => !string.IsNullOrEmpty(x)));
    }

    /// <summary>Work returned to an engineer, with what the reviewer said.</summary>
    public sealed record ReturnedItem(
        int ProjectId,
        string ProjectCode,
        string Sample,
        string? PartName,
        int? SubcategoryId,
        int? TaskId,
        string? TaskName,
        int Round,
        string ReturnedByName,
        DateTime ReturnedAt,
        string? Comments,
        string? MarkupUrl,
        string? MarkupFileName)
    {
        public string ShortTitle => string.Join(" › ", new[] { Sample, PartName, TaskName }.Where(x => !string.IsNullOrEmpty(x)));
    }

    /// <summary>
    /// The checking workflow: Engineer → Checker → Project manager.
    /// A "part" is a task (projects with sub-categories) or the whole project when it has none.
    /// Engineers submit a part with a PDF. A checker sends it to the project manager or returns it
    /// to the engineer. The project manager approves it or returns it to the checker. Time spent
    /// reviewing is logged as Checking hours. A sub-category is approved when all of its tasks are;
    /// the project is Completed when all sub-categories are.
    /// </summary>
    public static class ProjectChecking
    {
        public static Task<bool> HasRoleAsync(ApplicationDbContext db, int projectId, string userId, string role) =>
            db.ProjectAssignments.AnyAsync(a => a.ProjectId == projectId && a.EmployeeId == userId && a.Role == role);

        public static string FormatTitle(string code, string sample, string? partName, string? taskName) =>
            string.Join(" › ", new[] { $"{code} · {sample}", partName, taskName }.Where(x => !string.IsNullOrEmpty(x)));

        /// <summary>Records a submission and locks the part. The PDF must already be stored.</summary>
        public static async Task<CheckResult> SubmitAsync(ApplicationDbContext db, string employeeId, int projectId, int? taskId,
            string pdfFileName, string pdfStoredName, long pdfSize, string? note, DateTime now)
        {
            if (!await HasRoleAsync(db, projectId, employeeId, RoleEngineer))
            {
                return CheckResult.Fail("Only this project's engineers can submit work for checking.");
            }

            var project = await LoadProjectAsync(db, projectId);
            if (project is null)
            {
                return CheckResult.Fail("This project no longer exists.");
            }
            if (!await db.ProjectAssignments.AnyAsync(a => a.ProjectId == projectId && a.Role == RoleChecker))
            {
                return CheckResult.Fail("No checker is assigned to this project yet. Ask your manager to add one.");
            }
            if (string.IsNullOrEmpty(project.ManagerId))
            {
                return CheckResult.Fail("This project has no project manager for the final check. Ask your manager to set one.");
            }

            var activeSubs = project.Subcategories.Where(s => !s.IsArchived).ToList();
            ProjectTask? task = null;
            if (taskId is int tid)
            {
                task = activeSubs.SelectMany(s => s.Tasks).FirstOrDefault(t => t.Id == tid && !t.IsArchived);
                if (task is null)
                {
                    return CheckResult.Fail("That task no longer exists.");
                }
                if (!CanSubmit(task.Status))
                {
                    return CheckResult.Fail($"{task.Name} is {task.Status.ToLowerInvariant()}, so it can't be submitted.");
                }
            }
            else
            {
                if (activeSubs.Count > 0)
                {
                    return CheckResult.Fail("Pick the task you're submitting.");
                }
                if (!CanSubmit(project.Status))
                {
                    return CheckResult.Fail($"This project is {project.Status.ToLowerInvariant()}, so it can't be submitted.");
                }
            }

            if (await db.CheckSubmissions.AnyAsync(c => c.ProjectId == projectId && c.TaskId == taskId && c.Result == null))
            {
                return CheckResult.Fail("This is already being checked.");
            }

            var round = await db.CheckSubmissions.CountAsync(c => c.ProjectId == projectId && c.TaskId == taskId) + 1;

            db.CheckSubmissions.Add(new CheckSubmission
            {
                ProjectId = projectId,
                SubcategoryId = task?.SubcategoryId,
                TaskId = taskId,
                Round = round,
                Stage = StageChecker,
                SubmittedById = employeeId,
                SubmittedAt = now,
                PdfFileName = TrimFileName(pdfFileName),
                PdfStoredName = pdfStoredName,
                PdfSize = pdfSize,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
            });

            if (task is not null)
            {
                task.Status = StatusForChecking;
                Recompute(project);
            }
            else
            {
                project.Status = StatusForChecking;
            }

            await db.SaveChangesAsync();

            // The part is locked now, so anyone modeling it stops.
            var working = task is not null
                ? await db.Users.AsNoTracking()
                    .Where(u => u.CurrentTaskId == task.Id && u.CurrentCheckId == null)
                    .Select(u => u.Id).ToListAsync()
                : await db.Users.AsNoTracking()
                    .Where(u => u.CurrentProjectId == projectId && u.CurrentSubcategoryId == null && u.CurrentCheckId == null)
                    .Select(u => u.Id).ToListAsync();
            foreach (var userId in working)
            {
                await ProjectWork.StopAsync(db, userId, now, clearCurrentProject: true);
            }

            return CheckResult.Success;
        }

        /// <summary>
        /// A reviewer picks up a submission: their status becomes "Checking · …" and their time is
        /// logged as Checking hours. Requires clock-in (checked by the caller).
        /// </summary>
        public static async Task<CheckResult> StartCheckingAsync(ApplicationDbContext db, string userId, int submissionId, DateTime now)
        {
            var submission = await db.CheckSubmissions
                .Include(c => c.Project)
                .Include(c => c.Subcategory)
                .Include(c => c.Task)
                .FirstOrDefaultAsync(c => c.Id == submissionId);

            var error = await ValidateReviewerAsync(db, submission, userId);
            if (error is not null) return CheckResult.Fail(error);

            if (StageOf(submission!) == StageChecker)
            {
                submission!.CheckerId = userId;
                submission.StartedAt ??= now;
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
            {
                return CheckResult.Fail("Your account wasn't found.");
            }

            var project = submission!.Project!;
            user.CurrentStatus = "Checking · " + FormatTitle(project.Code, project.Sample, submission.Subcategory?.Name, submission.Task?.Name);
            user.CurrentProjectId = submission.ProjectId;
            user.CurrentSubcategoryId = submission.SubcategoryId;
            user.CurrentTaskId = submission.TaskId;
            user.CurrentCheckId = submission.Id;
            await db.SaveChangesAsync();

            await ProjectWork.StartAsync(db, userId, submission.ProjectId, submission.SubcategoryId, now, WorkChecking, submission.TaskId);
            return CheckResult.Success;
        }

        /// <summary>
        /// Records a review decision. At the checker stage <paramref name="forward"/> sends it to the
        /// project manager (otherwise it goes back to the engineer). At the manager stage it approves
        /// the part (otherwise it goes back to the checker). Returning needs comments. The file is an
        /// updated PDF (checker sending it up) or a marked-up PDF, and must already be stored.
        /// </summary>
        public static async Task<CheckResult> ReviewAsync(ApplicationDbContext db, string userId, int submissionId, bool forward,
            string? comments, string? fileName, string? fileStoredName, long? fileSize, DateTime now)
        {
            var submission = await db.CheckSubmissions
                .Include(c => c.Task)
                .Include(c => c.Subcategory)
                .FirstOrDefaultAsync(c => c.Id == submissionId);
            if (submission is not null)
            {
                submission.Project = await LoadProjectAsync(db, submission.ProjectId);
            }

            var error = await ValidateReviewerAsync(db, submission, userId);
            if (error is not null) return CheckResult.Fail(error);

            comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim();
            if (!forward && comments is null)
            {
                return CheckResult.Fail("Add comments so they know what to fix.");
            }

            var project = submission!.Project!;
            var stage = StageOf(submission);
            string result;
            string newStatus;

            if (stage == StageChecker)
            {
                if (forward && string.IsNullOrEmpty(project.ManagerId))
                {
                    return CheckResult.Fail("This project has no project manager for the final check. Ask a manager to set one.");
                }

                submission.CheckerId = userId;
                submission.StartedAt ??= now;
                if (forward)
                {
                    result = ResultSentUp;
                    submission.Stage = StageManager;
                    newStatus = StatusManagerCheck;
                }
                else
                {
                    result = ResultReturned;
                    submission.Stage = StageClosed;
                    submission.Result = ResultReturned;
                    submission.ResultAt = now;
                    newStatus = StatusReturned;
                }
            }
            else
            {
                if (forward)
                {
                    result = ResultApproved;
                    submission.Stage = StageClosed;
                    submission.Result = ResultApproved;
                    submission.ResultAt = now;
                    newStatus = StatusApproved;
                }
                else
                {
                    result = ResultReturned;
                    submission.Stage = StageChecker;
                    newStatus = StatusReturnedByManager;
                }
            }

            db.CheckReviews.Add(new CheckReview
            {
                SubmissionId = submission.Id,
                Level = stage,
                ReviewerId = userId,
                Result = result,
                Comments = comments,
                FileName = string.IsNullOrEmpty(fileStoredName) ? null : TrimFileName(fileName ?? "file.pdf"),
                FileStoredName = string.IsNullOrEmpty(fileStoredName) ? null : fileStoredName,
                FileSize = string.IsNullOrEmpty(fileStoredName) ? null : fileSize,
                CreatedAt = now
            });

            SetPartStatus(submission, project, newStatus);
            await db.SaveChangesAsync();

            // Whoever was reviewing it goes back to Available.
            var reviewing = await db.Users
                .AsNoTracking()
                .Where(u => u.CurrentCheckId == submissionId)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var reviewerId in reviewing)
            {
                await ProjectWork.StopAsync(db, reviewerId, now, clearCurrentProject: true);
            }

            return CheckResult.Success;
        }

        /// <summary>Manager action: an approved task (or completed project without sub-categories) goes back to In Progress.</summary>
        public static async Task<CheckResult> ReopenAsync(ApplicationDbContext db, int projectId, int? taskId)
        {
            var project = await LoadProjectAsync(db, projectId);
            if (project is null)
            {
                return CheckResult.Fail("This project no longer exists.");
            }

            if (taskId is int tid)
            {
                var task = project.Subcategories.SelectMany(s => s.Tasks).FirstOrDefault(t => t.Id == tid);
                if (task is null || task.Status != StatusApproved)
                {
                    return CheckResult.Fail("Only approved tasks can be reopened.");
                }
                task.Status = StatusInProgress;
                Recompute(project);
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

        /// <summary>
        /// What's waiting for this person: submissions at the checker stage on projects they check
        /// (unclaimed, or claimed by them) and submissions at the manager stage on projects they manage.
        /// </summary>
        public static async Task<List<CheckQueueItem>> GetQueueAsync(ApplicationDbContext db, string userId)
        {
            var checkerProjects = await db.ProjectAssignments
                .AsNoTracking()
                .Where(a => a.EmployeeId == userId && a.Role == RoleChecker)
                .Select(a => a.ProjectId)
                .ToListAsync();
            var managedProjects = await db.Projects
                .AsNoTracking()
                .Where(p => p.ManagerId == userId)
                .Select(p => p.Id)
                .ToListAsync();
            if (checkerProjects.Count == 0 && managedProjects.Count == 0) return new();

            var open = await db.CheckSubmissions
                .AsNoTracking()
                .Include(c => c.Project)
                .Include(c => c.Subcategory)
                .Include(c => c.Task)
                .Include(c => c.Reviews)
                .Where(c => c.Result == null && (
                    ((c.Stage == null || c.Stage == StageChecker) && checkerProjects.Contains(c.ProjectId) && (c.CheckerId == null || c.CheckerId == userId))
                    || (c.Stage == StageManager && managedProjects.Contains(c.ProjectId))))
                .OrderBy(c => c.SubmittedAt)
                .ToListAsync();
            if (open.Count == 0) return new();

            var names = await GetNamesAsync(db);
            string NameOf(string? id) => id is not null && names.TryGetValue(id, out var n) ? n : "Unknown";

            return open.Select(c =>
            {
                var reviews = c.Reviews.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList();
                var stage = StageOf(c);
                var lastSentUp = reviews.LastOrDefault(r => r.Result == ResultSentUp);
                var lastReview = reviews.LastOrDefault();
                var managerReturn = stage == StageChecker && lastReview is not null && lastReview.Level == StageManager ? lastReview : null;

                // The PDF to review: the checker's updated PDF if they attached one, else the engineer's.
                var useCheckerFile = lastSentUp?.FileStoredName is not null;
                return new CheckQueueItem(
                    c.Id,
                    c.ProjectId,
                    c.Project!.Code,
                    c.Project.Sample,
                    c.Subcategory?.Name,
                    c.Task?.Name,
                    c.Round,
                    stage,
                    NameOf(c.SubmittedById),
                    c.SubmittedAt,
                    c.Note,
                    c.CheckerId is null ? null : NameOf(c.CheckerId),
                    lastSentUp?.Comments,
                    managerReturn?.Comments,
                    useCheckerFile ? $"/checks/review/{lastSentUp!.Id}" : $"/checks/{c.Id}/pdf",
                    useCheckerFile ? lastSentUp!.FileName ?? "file.pdf" : c.PdfFileName,
                    useCheckerFile ? lastSentUp!.FileSize ?? 0 : c.PdfSize,
                    managerReturn?.FileStoredName is null ? null : $"/checks/review/{managerReturn.Id}",
                    managerReturn?.FileName,
                    stage == StageChecker && c.CheckerId == userId && c.StartedAt is not null && managerReturn is null,
                    managerReturn is not null);
            }).ToList();
        }

        /// <summary>
        /// Tasks (and projects without sub-categories) returned to the engineers of projects this
        /// person works on, with the latest return comments.
        /// </summary>
        public static async Task<List<ReturnedItem>> GetReturnedAsync(ApplicationDbContext db, string engineerId)
        {
            var projectIds = await db.ProjectAssignments
                .AsNoTracking()
                .Where(a => a.EmployeeId == engineerId && a.Role == RoleEngineer)
                .Select(a => a.ProjectId)
                .ToListAsync();
            if (projectIds.Count == 0) return new();

            var returnedTaskIds = await db.ProjectTasks
                .AsNoTracking()
                .Where(t => projectIds.Contains(t.ProjectId) && !t.IsArchived && t.Status == StatusReturned)
                .Select(t => t.Id)
                .ToListAsync();
            var returnedProjectIds = await db.Projects
                .AsNoTracking()
                .Where(p => projectIds.Contains(p.Id) && p.Status == StatusReturned && !p.Subcategories.Any(s => !s.IsArchived))
                .Select(p => p.Id)
                .ToListAsync();
            if (returnedTaskIds.Count == 0 && returnedProjectIds.Count == 0) return new();

            // The latest returned submission for each part.
            var submissions = await db.CheckSubmissions
                .AsNoTracking()
                .Include(c => c.Project)
                .Include(c => c.Subcategory)
                .Include(c => c.Task)
                .Include(c => c.Reviews)
                .Where(c => c.Result == ResultReturned
                    && ((c.TaskId != null && returnedTaskIds.Contains(c.TaskId.Value))
                        || (c.TaskId == null && returnedProjectIds.Contains(c.ProjectId))))
                .ToListAsync();

            var names = await GetNamesAsync(db);
            string NameOf(string? id) => id is not null && names.TryGetValue(id, out var n) ? n : "Unknown";

            return submissions
                .GroupBy(c => (c.ProjectId, c.TaskId))
                .Select(g => g.OrderByDescending(c => c.ResultAt ?? c.SubmittedAt).First())
                .Select(c =>
                {
                    var review = c.Reviews.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).LastOrDefault(r => r.Result == ResultReturned && r.Level == StageChecker);
                    return new ReturnedItem(
                        c.ProjectId,
                        c.Project!.Code,
                        c.Project.Sample,
                        c.Subcategory?.Name,
                        c.SubcategoryId,
                        c.TaskId,
                        c.Task?.Name,
                        c.Round,
                        NameOf(review?.ReviewerId ?? c.CheckerId),
                        review?.CreatedAt ?? c.ResultAt ?? c.SubmittedAt,
                        review is not null ? review.Comments : c.Comments,
                        review is not null
                            ? (review.FileStoredName is null ? null : $"/checks/review/{review.Id}")
                            : (c.MarkupStoredName is null ? null : $"/checks/{c.Id}/markup"),
                        review is not null ? review.FileName : c.MarkupFileName);
                })
                .OrderByDescending(x => x.ReturnedAt)
                .ToList();
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
        /// Updates every sub-category's status from its tasks, then the project's status from its
        /// sub-categories. Requires Subcategories and their Tasks to be loaded.
        /// </summary>
        public static void Recompute(Project project)
        {
            var activeSubs = project.Subcategories.Where(s => !s.IsArchived).ToList();
            foreach (var sub in activeSubs)
            {
                var tasks = sub.Tasks.Where(t => !t.IsArchived).ToList();
                if (tasks.Count == 0)
                {
                    sub.Status = StatusInProgress;
                }
                else if (tasks.All(t => t.Status == StatusApproved))
                {
                    sub.Status = StatusApproved;
                }
                else if (tasks.All(t => t.Status == StatusApproved || IsInReview(t.Status)))
                {
                    sub.Status = StatusForChecking;
                }
                else
                {
                    sub.Status = StatusInProgress;
                }
            }

            if (activeSubs.Count == 0) return;

            if (activeSubs.All(s => s.Status == StatusApproved))
            {
                project.Status = StatusCompleted;
            }
            else if (activeSubs.All(s => s.Status == StatusApproved || s.Status == StatusForChecking))
            {
                project.Status = StatusForChecking;
            }
            else if (project.Status != StatusUnassigned && project.Status != StatusAssigned)
            {
                project.Status = StatusInProgress;
            }
        }

        /// <summary>
        /// One-time conversion of data from before tasks and two-level checking. Safe to run on every
        /// start: it only touches rows that still need converting.
        /// </summary>
        public static async Task BackfillAsync(ApplicationDbContext db, DateTime now)
        {
            // 1. The project manager defaults to whoever created the project.
            var noManager = await db.Projects.Where(p => p.ManagerId == null && p.CreatedById != null).ToListAsync();
            foreach (var p in noManager)
            {
                p.ManagerId = p.CreatedById;
            }

            // 2. Sub-categories that have hours or submissions without a task get one task with the
            //    same name, so existing hours and check history stay attached.
            var subIds = await db.ProjectTimeEntries.Where(t => t.SubcategoryId != null && t.TaskId == null).Select(t => t.SubcategoryId!.Value)
                .Union(db.CheckSubmissions.Where(c => c.SubcategoryId != null && c.TaskId == null).Select(c => c.SubcategoryId!.Value))
                .Distinct()
                .ToListAsync();
            if (subIds.Count > 0)
            {
                var subs = await db.ProjectSubcategories.Include(s => s.Tasks).Where(s => subIds.Contains(s.Id)).ToListAsync();
                foreach (var sub in subs)
                {
                    var task = sub.Tasks.FirstOrDefault(t => string.Equals(t.Name, sub.Name, StringComparison.OrdinalIgnoreCase));
                    if (task is null)
                    {
                        task = new ProjectTask
                        {
                            ProjectId = sub.ProjectId,
                            SubcategoryId = sub.Id,
                            Name = sub.Name,
                            IsArchived = sub.IsArchived,
                            Status = sub.Status switch
                            {
                                StatusForChecking => StatusForChecking,
                                StatusReturned => StatusReturned,
                                StatusApproved => StatusApproved,
                                _ => StatusInProgress
                            },
                            CreatedAt = now
                        };
                        sub.Tasks.Add(task);
                    }
                }
                await db.SaveChangesAsync();

                foreach (var sub in subs)
                {
                    var task = sub.Tasks.First(t => string.Equals(t.Name, sub.Name, StringComparison.OrdinalIgnoreCase));
                    var subId = sub.Id;
                    foreach (var entry in await db.ProjectTimeEntries.Where(t => t.SubcategoryId == subId && t.TaskId == null).ToListAsync())
                    {
                        entry.TaskId = task.Id;
                    }
                    foreach (var submission in await db.CheckSubmissions.Where(c => c.SubcategoryId == subId && c.TaskId == null).ToListAsync())
                    {
                        submission.TaskId = task.Id;
                    }
                    foreach (var user in await db.Users.Where(u => u.CurrentSubcategoryId == subId && u.CurrentTaskId == null).ToListAsync())
                    {
                        user.CurrentTaskId = task.Id;
                    }
                }
            }

            // 3. Sub-category statuses now come from their tasks (older ones may still say Returned).
            var staleSubs = await db.ProjectSubcategories
                .Include(s => s.Tasks)
                .Where(s => s.Status == StatusReturned || s.Status == StatusReturnedByManager || s.Status == StatusManagerCheck)
                .ToListAsync();
            foreach (var sub in staleSubs)
            {
                var tasks = sub.Tasks.Where(t => !t.IsArchived).ToList();
                sub.Status = tasks.Count > 0 && tasks.All(t => t.Status == StatusApproved) ? StatusApproved
                    : tasks.Count > 0 && tasks.All(t => t.Status == StatusApproved || IsInReview(t.Status)) ? StatusForChecking
                    : StatusInProgress;
            }

            // 4. Submissions from one-level checking get a stage.
            foreach (var submission in await db.CheckSubmissions.Where(c => c.Stage == null).ToListAsync())
            {
                submission.Stage = submission.Result is null ? StageChecker : StageClosed;
            }

            await db.SaveChangesAsync();
        }

        public static string StageOf(CheckSubmission submission) =>
            submission.Result is not null ? StageClosed : submission.Stage ?? StageChecker;

        private static Task<Project?> LoadProjectAsync(ApplicationDbContext db, int projectId) =>
            db.Projects
                .Include(p => p.Subcategories).ThenInclude(s => s.Tasks)
                .FirstOrDefaultAsync(p => p.Id == projectId);

        private static void SetPartStatus(CheckSubmission submission, Project project, string status)
        {
            if (submission.TaskId is int tid)
            {
                var task = project.Subcategories.SelectMany(s => s.Tasks).FirstOrDefault(t => t.Id == tid);
                if (task is not null)
                {
                    task.Status = status;
                    Recompute(project);
                    return;
                }
            }

            // A project without sub-categories is the part itself.
            project.Status = status == StatusApproved ? StatusCompleted : status;
        }

        private static async Task<string?> ValidateReviewerAsync(ApplicationDbContext db, CheckSubmission? submission, string userId)
        {
            if (submission?.Project is null) return "This submission no longer exists.";

            var stage = StageOf(submission);
            if (stage == StageClosed) return "This was already checked.";

            if (stage == StageManager)
            {
                return submission.Project.ManagerId == userId ? null : "Only this project's manager can do the final check.";
            }

            if (!await HasRoleAsync(db, submission.ProjectId, userId, RoleChecker)) return "You're not a checker on this project.";
            if (submission.CheckerId is not null && submission.CheckerId != userId) return "Another checker is already checking this.";
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
