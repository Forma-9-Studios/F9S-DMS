using System.ComponentModel.DataAnnotations;

namespace F9SDMS.Data
{
    /// <summary>
    /// One decision on a submission: the checker sending it to the project manager or returning
    /// it to the engineer, or the project manager approving it or returning it to the checker.
    /// </summary>
    public class CheckReview
    {
        [Key]
        public int Id { get; set; }

        public int SubmissionId { get; set; }

        public CheckSubmission? Submission { get; set; }

        /// <summary><see cref="ProjectOptions.StageChecker"/> or <see cref="ProjectOptions.StageManager"/>.</summary>
        [Required, MaxLength(20)]
        public string Level { get; set; } = ProjectOptions.StageChecker;

        [Required]
        public string ReviewerId { get; set; } = string.Empty;

        /// <summary>Sent to Manager, Returned or Approved.</summary>
        [Required, MaxLength(30)]
        public string Result { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string? Comments { get; set; }

        /// <summary>An updated PDF (checker sending it up) or a marked-up PDF (returns).</summary>
        [MaxLength(260)]
        public string? FileName { get; set; }

        [MaxLength(100)]
        public string? FileStoredName { get; set; }

        public long? FileSize { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
