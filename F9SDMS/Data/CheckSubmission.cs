using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>
    /// One round of checking: an engineer submits a part (a sub-category, or the whole project
    /// when it has none) with a PDF, and a checker approves it or returns it with comments.
    /// </summary>
    public class CheckSubmission
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public Project? Project { get; set; }

        /// <summary>The part submitted; null when the project has no sub-categories.</summary>
        public int? SubcategoryId { get; set; }

        public ProjectSubcategory? Subcategory { get; set; }

        /// <summary>1 for the first submission of this part, 2 after it was returned once, and so on.</summary>
        public int Round { get; set; }

        [Required]
        public string SubmittedById { get; set; } = string.Empty;

        public DateTime SubmittedAt { get; set; }

        /// <summary>The PDF's original file name, shown to people.</summary>
        [Required, MaxLength(260)]
        public string PdfFileName { get; set; } = string.Empty;

        /// <summary>The PDF's name on disk (see CheckFileStore).</summary>
        [Required, MaxLength(100)]
        public string PdfStoredName { get; set; } = string.Empty;

        public long PdfSize { get; set; }

        [MaxLength(2000)]
        public string? Note { get; set; }

        /// <summary>The checker who picked it up (or gave the result).</summary>
        public string? CheckerId { get; set; }

        public DateTime? StartedAt { get; set; }

        /// <summary>Null while waiting or being checked; otherwise Approved or Returned.</summary>
        [MaxLength(20)]
        public string? Result { get; set; }

        public DateTime? ResultAt { get; set; }

        [MaxLength(4000)]
        public string? Comments { get; set; }

        [MaxLength(260)]
        public string? MarkupFileName { get; set; }

        [MaxLength(100)]
        public string? MarkupStoredName { get; set; }

        public long? MarkupSize { get; set; }

        public bool IsOpen => Result is null;
    }
}
