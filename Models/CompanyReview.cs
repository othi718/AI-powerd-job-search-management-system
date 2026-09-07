// Models/CompanyReview.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public class CompanyReview
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }
        [ForeignKey(nameof(CompanyId))]
        public Company? Company { get; set; }

        public string JobSeekerUserId { get; set; } = string.Empty;
        [ForeignKey(nameof(JobSeekerUserId))]
        public ApplicationUser? JobSeekerUser { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}