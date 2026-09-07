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
        public string? Insights { get; set; }
        public bool IsApproved { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? ContactEmail { get; set; }
        public string? Phone { get; set; }
        public string? Location { get; set; }

        public ICollection<Employer> Employers { get; set; } = new List<Employer>();
        public ICollection<Branch> Branches { get; set; } = new List<Branch>();
        public ICollection<CompanyFollow> Followers { get; set; } = new List<CompanyFollow>();
        public ICollection<CompanyReview> Reviews { get; set; } = new List<CompanyReview>();
    }
}