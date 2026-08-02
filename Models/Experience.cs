using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public class Experience
    {
        public int Id { get; set; }

        [Required]
        public int ResumeId { get; set; }
        [ForeignKey(nameof(ResumeId))]
        public Resume? Resume { get; set; }

        [Required, MaxLength(150)]
        public string CompanyName { get; set; } = string.Empty;
        [Required, MaxLength(150)]
        public string JobTitle { get; set; } = string.Empty;

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public string? Description { get; set; }
    }
}