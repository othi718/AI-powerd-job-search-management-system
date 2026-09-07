using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace AI_powerd_job_search_management_system.Models
{
    public class Employer
    {
        public int Id { get; set; }
        [Required]
        public string ApplicationUserId { get; set; } = string.Empty;
        [ForeignKey(nameof(ApplicationUserId))]
        public ApplicationUser? ApplicationUser { get; set; }

        [Required]
        public int CompanyId { get; set; }
        [ForeignKey(nameof(CompanyId))]
        public Company? Company { get; set; }

        // NEW — every Employer (HR/Manager) belongs to exactly one branch
      
        public int BranchId { get; set; }
        [ForeignKey(nameof(BranchId))]
        public Branch? Branch { get; set; }
        public EmployerPosition Position { get; set; } = EmployerPosition.Owner;
        public bool IsApprovedByOwner { get; set; } = false;

        [Required, MaxLength(100)]
        public string JobTitle { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Job> Jobs { get; set; } = new List<Job>();
    }
}