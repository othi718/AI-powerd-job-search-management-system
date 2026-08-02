using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public class CandidateSkill
    {
        public int Id { get; set; }

        public int ResumeId { get; set; }
        [ForeignKey(nameof(ResumeId))]
        public Resume? Resume { get; set; }

        public int SkillId { get; set; }
        [ForeignKey(nameof(SkillId))]
        public Skill? Skill { get; set; }

        public string? ProficiencyLevel { get; set; }
    }
}