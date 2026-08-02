using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public enum ApplicationStatus { Pending, Shortlisted, Rejected, Accepted }

    public class JobApplication
    {
        public int Id { get; set; }

        [Required]
        public int JobId { get; set; }
        [ForeignKey(nameof(JobId))]
        public Job? Job { get; set; }

        [Required]
        public int JobSeekerId { get; set; }
        [ForeignKey(nameof(JobSeekerId))]
        public JobSeeker? JobSeeker { get; set; }

        [Required]
        public int ResumeId { get; set; }
        [ForeignKey(nameof(ResumeId))]
        public Resume? Resume { get; set; }

        public double MatchScore { get; set; } = 0;

        public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

        public AIAnalysis? AIAnalysis { get; set; }
        public Interview? Interview { get; set; }
    }
}
