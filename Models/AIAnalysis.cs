using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public class AIAnalysis
    {
        public int Id { get; set; }

        [Required]
        public int JobApplicationId { get; set; }
        [ForeignKey(nameof(JobApplicationId))]
        public JobApplication? JobApplication { get; set; }

        public string? MatchedSkills { get; set; }
        public string? MissingSkills { get; set; }

        public double SkillMatchScore { get; set; }
        public double EducationExperienceScore { get; set; }
        public double OverallScore { get; set; }

        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    }
}