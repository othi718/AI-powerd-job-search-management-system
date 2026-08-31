using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AI_powerd_job_search_management_system.Data;
using AI_powerd_job_search_management_system.Models;

namespace AI_powerd_job_search_management_system.Controllers
{
    [Authorize]
    public class FilesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public FilesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        public async Task<IActionResult> DownloadResume(int id)
        {
            var resume = await _context.Resumes.Include(r => r.JobSeeker).FirstOrDefaultAsync(r => r.Id == id);
            if (resume == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            bool isOwner = resume.JobSeeker!.ApplicationUserId == userId;
            bool isEmployerOfApplicant = await _context.JobApplications
                .Include(a => a.Job).ThenInclude(j => j!.Employer)
                .AnyAsync(a => a.ResumeId == id && a.Job!.Employer!.ApplicationUserId == userId);

            if (!isOwner && !isEmployerOfApplicant) return Forbid();

            var fullPath = Path.Combine(_env.ContentRootPath, "UploadedFiles", "Resumes", resume.FilePath);
            if (!System.IO.File.Exists(fullPath)) return NotFound();

            var contentType = resume.FilePath.EndsWith(".pdf")
                ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

            return PhysicalFile(fullPath, contentType, resume.OriginalFileName);
        }
    }
}