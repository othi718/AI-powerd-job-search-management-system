using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public class Certification
    {
        public int Id { get; set; }

        [Required]
        public int ResumeId { get; set; }
        [ForeignKey(nameof(ResumeId))]
        public Resume? Resume { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(200)]
        public string? IssuingOrganization { get; set; }

        public DateTime? DateIssued { get; set; }
    }
}