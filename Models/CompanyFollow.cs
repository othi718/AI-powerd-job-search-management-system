using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public class CompanyFollow
    {
        public int Id { get; set; }

        [Required]
        public string JobSeekerUserId { get; set; } = string.Empty;
        [ForeignKey(nameof(JobSeekerUserId))]
        public ApplicationUser? JobSeekerUser { get; set; }

        [Required]
        public int CompanyId { get; set; }
        [ForeignKey(nameof(CompanyId))]
        public Company? Company { get; set; }

        public DateTime FollowedAt { get; set; } = DateTime.UtcNow;
    }
}