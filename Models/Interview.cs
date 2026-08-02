using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public enum InterviewStatus { Scheduled, Completed, Cancelled }

    public class Interview
    {
        public int Id { get; set; }

        [Required]
        public int JobApplicationId { get; set; }
        [ForeignKey(nameof(JobApplicationId))]
        public JobApplication? JobApplication { get; set; }

        public DateTime ScheduledAt { get; set; }
        public string? Location { get; set; }

        public InterviewStatus Status { get; set; } = InterviewStatus.Scheduled;
        public string? Notes { get; set; }
    }
}
