using System.ComponentModel.DataAnnotations;
using AI_powerd_job_search_management_system.Models;

namespace AI_powerd_job_search_management_system.ViewModels
{
    public class CompanyViewModel
    {
        public Company Company { get; set; } = null!;
        public List<Job> Jobs { get; set; } = new();
        public List<Employer> Employees { get; set; } = new();
    }

    public class AddEmployeeViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string JobTitle { get; set; } = string.Empty;
    }
}