using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public class JobSkill
    {
        public int Id { get; set; }

        public int JobId { get; set; }
        [ForeignKey(nameof(JobId))]
        public Job? Job { get; set; }

        public int SkillId { get; set; }
        [ForeignKey(nameof(SkillId))]
        public Skill? Skill { get; set; }

        public bool IsRequired { get; set; } = true;
    }
}
