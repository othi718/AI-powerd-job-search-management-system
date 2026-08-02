using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public class SavedJob
    {
        public int Id { get; set; }

        public int JobSeekerId { get; set; }
        [ForeignKey(nameof(JobSeekerId))]
        public JobSeeker? JobSeeker { get; set; }

        public int JobId { get; set; }
        [ForeignKey(nameof(JobId))]
        public Job? Job { get; set; }

        public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    }
}
