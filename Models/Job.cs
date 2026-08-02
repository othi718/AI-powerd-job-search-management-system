using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Builder;

namespace AI_powerd_job_search_management_system.Models
{
    public enum JobStatus { Open, Closed }

    public class Job
    {
        public int Id { get; set; }

        [Required]
        public int EmployerId { get; set; }
        [ForeignKey(nameof(EmployerId))]
        public Employer? Employer { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        [Required]
        public string Description { get; set; } = string.Empty;

        public string? Location { get; set; }
        public string? SalaryRange { get; set; }

        public JobStatus Status { get; set; } = JobStatus.Open;
        public DateTime PostedAt { get; set; } = DateTime.UtcNow;

        public ICollection<JobSkill> JobSkills { get; set; } = new List<JobSkill>();
        public ICollection<JobApplication> Applications { get; set; } = new List<JobApplication>();
        public ICollection<SavedJob> SavedByJobSeekers { get; set; } = new List<SavedJob>();
    }
}