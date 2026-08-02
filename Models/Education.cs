using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public class Education
    {
        public int Id { get; set; }

        [Required]
        public int ResumeId { get; set; }
        [ForeignKey(nameof(ResumeId))]
        public Resume? Resume { get; set; }

        [Required, MaxLength(200)]
        public string Institution { get; set; } = string.Empty;
        [MaxLength(150)]
        public string? Degree { get; set; }
        [MaxLength(150)]
        public string? FieldOfStudy { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}