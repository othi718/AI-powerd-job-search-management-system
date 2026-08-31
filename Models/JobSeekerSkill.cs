using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public class JobSeekerSkill
    {
        public int Id { get; set; }

        [Required]
        public int JobSeekerId { get; set; }

        [ForeignKey(nameof(JobSeekerId))]
        public JobSeeker? JobSeeker { get; set; }

        [Required]
        public int SkillId { get; set; }

        [ForeignKey(nameof(SkillId))]
        public Skill? Skill { get; set; }
    }
}
