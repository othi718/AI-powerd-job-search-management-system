using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public class Employer
    {
        public int Id { get; set; }

        [Required]
        public string ApplicationUserId { get; set; } = string.Empty;
        [ForeignKey(nameof(ApplicationUserId))]
        public ApplicationUser? ApplicationUser { get; set; }

        [Required]
        public int CompanyId { get; set; }
        [ForeignKey(nameof(CompanyId))]
        public Company? Company { get; set; }

        [Required, MaxLength(100)]
        public string JobTitle { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Job> Jobs { get; set; } = new List<Job>();
    }
}