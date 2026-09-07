using System.ComponentModel.DataAnnotations;

namespace AI_powerd_job_search_management_system.ViewModels
{
    public class CompleteEmployerProfileViewModel
    {
        [Required, MaxLength(200)]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;

        public string? Website { get; set; }

        [Required, EmailAddress]
        [Display(Name = "Company Contact Email")]
        public string ContactEmail { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string Location { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        [Display(Name = "Your Job Title")]
        public string JobTitle { get; set; } = string.Empty;
    }
}