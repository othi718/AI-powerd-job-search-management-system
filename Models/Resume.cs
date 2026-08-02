using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public class Resume
    {
        public int Id { get; set; }

        [Required]
        public int JobSeekerId { get; set; }
        [ForeignKey(nameof(JobSeekerId))]
        public JobSeeker? JobSeeker { get; set; }

        [Required]
        public string FilePath { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;

        public string? ParsedFullName { get; set; }
        public string? ParsedEmail { get; set; }
        public string? ParsedPhone { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Education> Educations { get; set; } = new List<Education>();
        public ICollection<Experience> Experiences { get; set; } = new List<Experience>();
        public ICollection<Certification> Certifications { get; set; } = new List<Certification>();
        public ICollection<Project> Projects { get; set; } = new List<Project>();
        public ICollection<ExtracurricularActivity> ExtracurricularActivities { get; set; } = new List<ExtracurricularActivity>();
        public ICollection<CandidateSkill> CandidateSkills { get; set; } = new List<CandidateSkill>();
    }
}