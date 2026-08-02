using System.ComponentModel.DataAnnotations;

namespace AI_powerd_job_search_management_system.Models
{
    public class Company
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        public string? Website { get; set; }
        public string? LogoPath { get; set; }

        public bool IsApproved { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Employer> Employers { get; set; } = new List<Employer>();
    }
}