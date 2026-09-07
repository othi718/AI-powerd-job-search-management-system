using AI_Powered_Smart_Job_Management_System.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public class InterviewMessage
    {
        public int Id { get; set; }

        [Required]
        public int JobApplicationId { get; set; }
        [ForeignKey(nameof(JobApplicationId))]
        public JobApplication? JobApplication { get; set; }

        [Required]
        public string SenderUserId { get; set; } = string.Empty;
        [ForeignKey(nameof(SenderUserId))]
        public ApplicationUser? Sender { get; set; }

        [Required]
        public string Message { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;

    }
}
