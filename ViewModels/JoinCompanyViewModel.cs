using System.ComponentModel.DataAnnotations;
using AI_powerd_job_search_management_system.Models;

namespace AI_powerd_job_search_management_system.ViewModels
{
    public class JoinCompanyViewModel
    {
        [Required]
        [Display(Name = "Company")]
        public int CompanyId { get; set; }

        [Required]
        public EmployerPosition Position { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Your Job Title")]
        public string JobTitle { get; set; } = string.Empty;
    }
}