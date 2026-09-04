using System.ComponentModel.DataAnnotations;

namespace AI_powerd_job_search_management_system.ViewModels
{
    public class JobViewModel
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public string? Location { get; set; }

        [Display(Name = "Salary Range")]
        public string? SalaryRange { get; set; }
        [Display(Name = "Required Skills")]
        [Required]
        public string RequiredSkills { get; set; } = string.Empty;
        [Required]
        public string Category { get; set; } = string.Empty;
    }
}