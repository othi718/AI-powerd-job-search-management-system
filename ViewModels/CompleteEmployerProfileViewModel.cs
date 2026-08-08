using System.ComponentModel.DataAnnotations;

namespace AI_powerd_job_search_management_system.ViewModels
{
    public class CompleteEmployerProfileViewModel
    {
        [Required, MaxLength(200)]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;

        public string? Website { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Your Job Title")]
        public string JobTitle { get; set; } = string.Empty;   // e.g. "HR Manager"
    }
}