using System.ComponentModel.DataAnnotations;

namespace AI_powerd_job_search_management_system.ViewModels
{
    public class ResumeUploadViewModel
    {
        public int? ResumeId { get; set; }

        [Required(ErrorMessage = "Please select a resume file.")]
        [Display(Name = "Resume File (PDF or DOCX)")]
        public IFormFile File { get; set; } = null!;
    }
}