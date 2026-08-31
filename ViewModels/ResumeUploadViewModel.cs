using System.ComponentModel.DataAnnotations;

namespace AI_powerd_job_search_management_system.ViewModels
{
    public class ResumeUploadViewModel
    {
        [Required]
        [Display(Name = "Resume File (PDF or DOCX)")]
        public IFormFile File { get; set; } = null!;
    }
}