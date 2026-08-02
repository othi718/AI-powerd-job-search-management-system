using System.ComponentModel.DataAnnotations;

namespace AI_powerd_job_search_management_system.Models
{
    public class Skill
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public ICollection<CandidateSkill> CandidateSkills { get; set; } = new List<CandidateSkill>();
        public ICollection<JobSkill> JobSkills { get; set; } = new List<JobSkill>();
    }
}