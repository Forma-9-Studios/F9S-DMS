using ClosedXML.Excel;
using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    /// <summary>
    /// Builds an Excel workbook of a project's hours with four sheets: Summary (per sub-category,
    /// split into Modeling and Checking), Tasks, By person (person × sub-category), Time log and Check history.
    /// </summary>
    public static class ProjectExcelExport
    {
        private const string DurationFormat = "[h]:mm:ss";
        private const string GeneralLabel = "General (no sub-category)";

        public static async Task<(byte[] Content, string FileName)?> BuildAsync(ApplicationDbContext db, int projectId, DateTime now)
        {
            var project = await db.Projects
                .AsNoTracking()
                .Include(p => p.Subcategories)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project is null) return null;

            var entries = await db.ProjectTimeEntries
                .AsNoTracking()
                .Where(t => t.ProjectId == projectId)
                .OrderBy(t => t.StartTime)
                .ToListAsync();

            var names = await db.Users
                .AsNoTracking()
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToListAsync();
            var nameById = names.ToDictionary(u => u.Id, u =>
            {
                var n = $"{u.FirstName} {u.LastName}".Trim();
                return string.IsNullOrEmpty(n) ? (u.Email ?? "Unknown") : n;
            });

            var subNameById = project.Subcategories.ToDictionary(s => s.Id, s => s.IsArchived ? $"{s.Name} (archived)" : s.Name);

            var tasks = await db.ProjectTasks.AsNoTracking().Where(t => t.ProjectId == projectId).OrderBy(t => t.Id).ToListAsync();
            var taskById = tasks.ToDictionary(t => t.Id);
            string TaskName(int? id) => id is int i && taskById.TryGetValue(i, out var t) ? (t.IsArchived ? $"{t.Name} (archived)" : t.Name) : "";

            string SubName(int? id) => id is int i && subNameById.TryGetValue(i, out var n) ? n : GeneralLabel;
            string PersonName(string id) => nameById.TryGetValue(id, out var n) ? n : "Unknown";
            TimeSpan Duration(ProjectTimeEntry t)
            {
                var d = (t.EndTime ?? now) - t.StartTime;
                return d < TimeSpan.Zero ? TimeSpan.Zero : d;
            }
            bool IsChecking(ProjectTimeEntry t) => t.WorkType == ProjectOptions.WorkChecking;
            TimeSpan Sum(IEnumerable<ProjectTimeEntry> list) => list.Aggregate(TimeSpan.Zero, (s, e) => s + Duration(e));
            var statusById = project.Subcategories.ToDictionary(s => s.Id, s => s.IsArchived ? "Archived" : s.Status);
            var hasSubs = project.Subcategories.Any(s => !s.IsArchived);

            // Sub-category order: the project's own order, then "General" if any time has none.
            var subColumns = project.Subcategories.OrderBy(s => s.Id).Select(s => (int?)s.Id).ToList();
            if (entries.Any(e => e.SubcategoryId is null)) subColumns.Add(null);

            using var wb = new XLWorkbook();

            // ---- Summary
            var summary = wb.Worksheets.Add("Summary");
            summary.Cell(1, 1).Value = $"{project.Code} · {project.Sample}";
            summary.Cell(1, 1).Style.Font.Bold = true;
            summary.Cell(1, 1).Style.Font.FontSize = 14;

            var info = new (string Label, string Value)[]
            {
                ("Category", project.Category ?? "—"),
                ("LOD", project.Lod ?? "—"),
                ("Status", project.Status),
                ("Due date", project.DueDate.ToString("MMMM d, yyyy")),
                ("Exported", now.ToString("MMMM d, yyyy h:mm tt"))
            };
            var row = 3;
            foreach (var (label, value) in info)
            {
                summary.Cell(row, 1).Value = label;
                summary.Cell(row, 1).Style.Font.Bold = true;
                summary.Cell(row, 2).Value = value;
                row++;
            }

            row++;
            var headerRow = row;
            var summaryHeaders = new[] { "Sub-category", "Status", "Modeling", "Checking", "Total (h:mm:ss)", "Total (decimal)" };
            for (var c = 0; c < summaryHeaders.Length; c++) summary.Cell(row, c + 1).Value = summaryHeaders[c];
            StyleHeader(summary.Range(row, 1, row, summaryHeaders.Length));
            row++;

            TimeSpan totalModeling = TimeSpan.Zero, totalChecking = TimeSpan.Zero;
            foreach (var sub in subColumns)
            {
                var subEntries = entries.Where(e => e.SubcategoryId == sub).ToList();
                var modeling = Sum(subEntries.Where(e => !IsChecking(e)));
                var checking = Sum(subEntries.Where(IsChecking));
                totalModeling += modeling;
                totalChecking += checking;

                summary.Cell(row, 1).Value = SubName(sub);
                summary.Cell(row, 2).Value = sub is int sid && statusById.TryGetValue(sid, out var st) ? st : hasSubs ? "" : project.Status;
                SetDuration(summary.Cell(row, 3), modeling);
                SetDuration(summary.Cell(row, 4), checking);
                SetDuration(summary.Cell(row, 5), modeling + checking);
                summary.Cell(row, 6).Value = Math.Round((modeling + checking).TotalHours, 2);
                row++;
            }

            summary.Cell(row, 1).Value = "Total";
            SetDuration(summary.Cell(row, 3), totalModeling);
            SetDuration(summary.Cell(row, 4), totalChecking);
            SetDuration(summary.Cell(row, 5), totalModeling + totalChecking);
            summary.Cell(row, 6).Value = Math.Round((totalModeling + totalChecking).TotalHours, 2);
            summary.Range(row, 1, row, 6).Style.Font.Bold = true;
            summary.Range(row, 1, row, 6).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            summary.Range(headerRow, 6, row, 6).Style.NumberFormat.Format = "0.00";
            summary.Columns().AdjustToContents();

            // ---- Tasks
            if (tasks.Count > 0)
            {
                var taskSheet = wb.Worksheets.Add("Tasks");
                var taskHeaders = new[] { "Sub-category", "Task", "Status", "Modeling", "Checking", "Total (h:mm:ss)", "Total (decimal)" };
                for (var c = 0; c < taskHeaders.Length; c++) taskSheet.Cell(1, c + 1).Value = taskHeaders[c];
                StyleHeader(taskSheet.Range(1, 1, 1, taskHeaders.Length));

                var taskRow = 2;
                foreach (var task in tasks.OrderBy(t => t.SubcategoryId).ThenBy(t => t.Id))
                {
                    var taskEntries = entries.Where(e => e.TaskId == task.Id).ToList();
                    var modeling = Sum(taskEntries.Where(e => !IsChecking(e)));
                    var checking = Sum(taskEntries.Where(IsChecking));
                    taskSheet.Cell(taskRow, 1).Value = SubName(task.SubcategoryId);
                    taskSheet.Cell(taskRow, 2).Value = TaskName(task.Id);
                    taskSheet.Cell(taskRow, 3).Value = task.Status;
                    SetDuration(taskSheet.Cell(taskRow, 4), modeling);
                    SetDuration(taskSheet.Cell(taskRow, 5), checking);
                    SetDuration(taskSheet.Cell(taskRow, 6), modeling + checking);
                    taskSheet.Cell(taskRow, 7).Value = Math.Round((modeling + checking).TotalHours, 2);
                    taskSheet.Cell(taskRow, 7).Style.NumberFormat.Format = "0.00";
                    taskRow++;
                }
                taskSheet.Range(1, 1, taskRow - 1, taskHeaders.Length).SetAutoFilter();
                taskSheet.Columns().AdjustToContents();
            }

            // ---- By person
            var byPerson = wb.Worksheets.Add("By person");
            byPerson.Cell(1, 1).Value = "BIM Engineer / Architect";
            for (var c = 0; c < subColumns.Count; c++)
            {
                byPerson.Cell(1, c + 2).Value = SubName(subColumns[c]);
            }
            var modelingCol = subColumns.Count + 2;
            byPerson.Cell(1, modelingCol).Value = "Modeling";
            byPerson.Cell(1, modelingCol + 1).Value = "Checking";
            byPerson.Cell(1, modelingCol + 2).Value = "Total";
            StyleHeader(byPerson.Range(1, 1, 1, modelingCol + 2));

            var people = entries.Select(e => e.EmployeeId).Distinct().OrderBy(PersonName).ToList();
            row = 2;
            foreach (var person in people)
            {
                byPerson.Cell(row, 1).Value = PersonName(person);
                var personTotal = TimeSpan.Zero;
                for (var c = 0; c < subColumns.Count; c++)
                {
                    var hours = entries.Where(e => e.EmployeeId == person && e.SubcategoryId == subColumns[c])
                        .Aggregate(TimeSpan.Zero, (s, e) => s + Duration(e));
                    personTotal += hours;
                    SetDuration(byPerson.Cell(row, c + 2), hours);
                }
                var personEntries = entries.Where(e => e.EmployeeId == person).ToList();
                SetDuration(byPerson.Cell(row, modelingCol), Sum(personEntries.Where(e => !IsChecking(e))));
                SetDuration(byPerson.Cell(row, modelingCol + 1), Sum(personEntries.Where(IsChecking)));
                SetDuration(byPerson.Cell(row, modelingCol + 2), personTotal);
                byPerson.Cell(row, modelingCol + 2).Style.Font.Bold = true;
                row++;
            }
            byPerson.Columns().AdjustToContents();

            // ---- Time log
            var log = wb.Worksheets.Add("Time log");
            var headers = new[] { "Person", "Sub-category", "Task", "Type", "Date", "Start", "Stop", "Hours" };
            for (var c = 0; c < headers.Length; c++) log.Cell(1, c + 1).Value = headers[c];
            StyleHeader(log.Range(1, 1, 1, headers.Length));

            row = 2;
            foreach (var e in entries)
            {
                log.Cell(row, 1).Value = PersonName(e.EmployeeId);
                log.Cell(row, 2).Value = SubName(e.SubcategoryId);
                log.Cell(row, 3).Value = TaskName(e.TaskId);
                log.Cell(row, 4).Value = IsChecking(e) ? ProjectOptions.WorkChecking : ProjectOptions.WorkModeling;
                log.Cell(row, 5).Value = e.StartTime.Date;
                log.Cell(row, 5).Style.NumberFormat.Format = "mmm d, yyyy";
                log.Cell(row, 6).Value = e.StartTime;
                log.Cell(row, 6).Style.NumberFormat.Format = "h:mm:ss AM/PM";
                if (e.EndTime is DateTime end)
                {
                    log.Cell(row, 7).Value = end;
                    log.Cell(row, 7).Style.NumberFormat.Format = "h:mm:ss AM/PM";
                }
                else
                {
                    log.Cell(row, 7).Value = "Working now";
                }
                SetDuration(log.Cell(row, 8), Duration(e));
                row++;
            }
            if (entries.Count > 0)
            {
                log.Range(1, 1, row - 1, headers.Length).SetAutoFilter();
            }
            log.Columns().AdjustToContents();

            // ---- Check history (one row per step: engineer submission, checker and manager decisions)
            var submissions = await db.CheckSubmissions
                .AsNoTracking()
                .Include(c => c.Reviews)
                .Where(c => c.ProjectId == projectId)
                .OrderBy(c => c.SubmittedAt)
                .ToListAsync();
            var checks = wb.Worksheets.Add("Check history");
            var checkHeaders = new[] { "Sub-category", "Task", "Round", "Step", "Person", "Date", "Result", "Note / comments", "PDF" };
            for (var c = 0; c < checkHeaders.Length; c++) checks.Cell(1, c + 1).Value = checkHeaders[c];
            StyleHeader(checks.Range(1, 1, 1, checkHeaders.Length));

            row = 2;
            void AddStep(CheckSubmission c, string step, string? personId, DateTime when, string result, string? text, string? file)
            {
                checks.Cell(row, 1).Value = c.SubcategoryId is null ? "Whole project" : SubName(c.SubcategoryId);
                checks.Cell(row, 2).Value = TaskName(c.TaskId);
                checks.Cell(row, 3).Value = c.Round;
                checks.Cell(row, 4).Value = step;
                checks.Cell(row, 5).Value = personId is null ? "" : PersonName(personId);
                checks.Cell(row, 6).Value = when;
                checks.Cell(row, 6).Style.NumberFormat.Format = "mmm d, yyyy h:mm AM/PM";
                checks.Cell(row, 7).Value = result;
                checks.Cell(row, 8).Value = text ?? "";
                checks.Cell(row, 9).Value = file ?? "";
                row++;
            }

            foreach (var c in submissions)
            {
                AddStep(c, "Engineer", c.SubmittedById, c.SubmittedAt, "Submitted", c.Note, c.PdfFileName);
                if (c.Reviews.Count > 0)
                {
                    foreach (var r in c.Reviews.OrderBy(x => x.CreatedAt))
                    {
                        var result = (r.Level, r.Result) switch
                        {
                            (ProjectOptions.StageChecker, ProjectOptions.ResultSentUp) => "Sent to Manager",
                            (ProjectOptions.StageChecker, _) => "Returned to engineer",
                            (_, ProjectOptions.ResultApproved) => "Approved",
                            _ => "Returned to Checker"
                        };
                        AddStep(c, r.Level == ProjectOptions.StageManager ? "Manager" : "Checker", r.ReviewerId, r.CreatedAt, result, r.Comments, r.FileName);
                    }
                }
                else if (c.Result is not null && c.ResultAt is DateTime checkedAt)
                {
                    AddStep(c, "Checker", c.CheckerId, checkedAt, c.Result, c.Comments, c.MarkupFileName);
                }

                if (c.Result is null)
                {
                    var stage = ProjectChecking.StageOf(c);
                    checks.Cell(row - 1, 7).Value = checks.Cell(row - 1, 7).GetString() + (stage == ProjectOptions.StageManager ? " · waiting for the manager" : " · waiting for the checker");
                }
            }
            checks.Columns().AdjustToContents();
            checks.Column(8).Width = Math.Min(checks.Column(8).Width, 60);
            checks.Column(8).Style.Alignment.WrapText = true;

            using var stream = new MemoryStream();
            wb.SaveAs(stream);

            var safeSample = string.Concat(project.Sample.Where(ch => char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-')).Trim();
            if (string.IsNullOrEmpty(safeSample)) safeSample = "project";
            var fileName = $"{project.Code} {safeSample} hours {now:yyyy-MM-dd}.xlsx";

            return (stream.ToArray(), fileName);
        }

        private static void SetDuration(IXLCell cell, TimeSpan span)
        {
            // Stored as a fraction of a day so Excel can add the hours up.
            cell.Value = span.TotalDays;
            cell.Style.NumberFormat.Format = DurationFormat;
        }

        private static void StyleHeader(IXLRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#098993");
        }
    }
}
