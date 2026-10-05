using ClosedXML.Excel;
using F9SDMS.Data;
using Microsoft.EntityFrameworkCore;

namespace F9SDMS.Services
{
    /// <summary>
    /// Builds an Excel workbook of a project's hours with three sheets:
    /// Summary (total + per sub-category), By person (person × sub-category) and Time log.
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

            string SubName(int? id) => id is int i && subNameById.TryGetValue(i, out var n) ? n : GeneralLabel;
            string PersonName(string id) => nameById.TryGetValue(id, out var n) ? n : "Unknown";
            TimeSpan Duration(ProjectTimeEntry t)
            {
                var d = (t.EndTime ?? now) - t.StartTime;
                return d < TimeSpan.Zero ? TimeSpan.Zero : d;
            }

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
            summary.Cell(row, 1).Value = "Sub-category";
            summary.Cell(row, 2).Value = "Hours (h:mm:ss)";
            summary.Cell(row, 3).Value = "Hours (decimal)";
            StyleHeader(summary.Range(row, 1, row, 3));
            row++;

            var total = TimeSpan.Zero;
            foreach (var sub in subColumns)
            {
                var hours = entries.Where(e => e.SubcategoryId == sub).Aggregate(TimeSpan.Zero, (s, e) => s + Duration(e));
                total += hours;
                summary.Cell(row, 1).Value = SubName(sub);
                SetDuration(summary.Cell(row, 2), hours);
                summary.Cell(row, 3).Value = Math.Round(hours.TotalHours, 2);
                row++;
            }

            summary.Cell(row, 1).Value = "Total";
            SetDuration(summary.Cell(row, 2), total);
            summary.Cell(row, 3).Value = Math.Round(total.TotalHours, 2);
            summary.Range(row, 1, row, 3).Style.Font.Bold = true;
            summary.Range(row, 1, row, 3).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            summary.Range(headerRow, 3, row, 3).Style.NumberFormat.Format = "0.00";
            summary.Columns().AdjustToContents();

            // ---- By person
            var byPerson = wb.Worksheets.Add("By person");
            byPerson.Cell(1, 1).Value = "BIM Engineer / Architect";
            for (var c = 0; c < subColumns.Count; c++)
            {
                byPerson.Cell(1, c + 2).Value = SubName(subColumns[c]);
            }
            byPerson.Cell(1, subColumns.Count + 2).Value = "Total";
            StyleHeader(byPerson.Range(1, 1, 1, subColumns.Count + 2));

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
                SetDuration(byPerson.Cell(row, subColumns.Count + 2), personTotal);
                byPerson.Cell(row, subColumns.Count + 2).Style.Font.Bold = true;
                row++;
            }
            byPerson.Columns().AdjustToContents();

            // ---- Time log
            var log = wb.Worksheets.Add("Time log");
            var headers = new[] { "BIM Engineer / Architect", "Sub-category", "Date", "Start", "Stop", "Hours" };
            for (var c = 0; c < headers.Length; c++) log.Cell(1, c + 1).Value = headers[c];
            StyleHeader(log.Range(1, 1, 1, headers.Length));

            row = 2;
            foreach (var e in entries)
            {
                log.Cell(row, 1).Value = PersonName(e.EmployeeId);
                log.Cell(row, 2).Value = SubName(e.SubcategoryId);
                log.Cell(row, 3).Value = e.StartTime.Date;
                log.Cell(row, 3).Style.NumberFormat.Format = "mmm d, yyyy";
                log.Cell(row, 4).Value = e.StartTime;
                log.Cell(row, 4).Style.NumberFormat.Format = "h:mm:ss AM/PM";
                if (e.EndTime is DateTime end)
                {
                    log.Cell(row, 5).Value = end;
                    log.Cell(row, 5).Style.NumberFormat.Format = "h:mm:ss AM/PM";
                }
                else
                {
                    log.Cell(row, 5).Value = "Working now";
                }
                SetDuration(log.Cell(row, 6), Duration(e));
                row++;
            }
            if (entries.Count > 0)
            {
                log.Range(1, 1, row - 1, headers.Length).SetAutoFilter();
            }
            log.Columns().AdjustToContents();

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
