using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public class Project
    {
        public int Id { get; set; }

        [Required]
        public int ResumeId { get; set; }
        [ForeignKey(nameof(ResumeId))]
        public Resume? Resume { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? TechnologiesUsed { get; set; }
    }
}