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

        private async Task<(Resume? resume, bool authorized)> AuthorizeResumeAsync(int id)
        {
            var resume = await _context.Resumes.Include(r => r.JobSeeker).FirstOrDefaultAsync(r => r.Id == id);
            if (resume == null) return (null, false);

            var userId = _userManager.GetUserId(User);
            bool isOwner = resume.JobSeeker!.ApplicationUserId == userId;
            bool isEmployerOfApplicant = await _context.JobApplications
                .Include(a => a.Job).ThenInclude(j => j!.Employer)
                .AnyAsync(a => a.ResumeId == id && a.Job!.Employer!.ApplicationUserId == userId);

            return (resume, isOwner || isEmployerOfApplicant);
        }

        public async Task<IActionResult> DownloadResume(int id)
        {
            var (resume, authorized) = await AuthorizeResumeAsync(id);
            if (resume == null) return NotFound();
            if (!authorized) return Forbid();

            var fullPath = Path.Combine(_env.ContentRootPath, "UploadedFiles", "Resumes", resume.FilePath);
            if (!System.IO.File.Exists(fullPath)) return NotFound();

            var contentType = resume.FilePath.EndsWith(".pdf")
                ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

            return PhysicalFile(fullPath, contentType, resume.OriginalFileName);
        }

        // Raw bytes, no attachment header — used internally by the Preview page (embed / fetch)
        public async Task<IActionResult> RawFile(int id)
        {
            var (resume, authorized) = await AuthorizeResumeAsync(id);
            if (resume == null) return NotFound();
            if (!authorized) return Forbid();

            var fullPath = Path.Combine(_env.ContentRootPath, "UploadedFiles", "Resumes", resume.FilePath);
            if (!System.IO.File.Exists(fullPath)) return NotFound();

            var contentType = resume.FilePath.EndsWith(".pdf")
                ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

            return PhysicalFile(fullPath, contentType);
        }

        // The combined "View" page: rendered CV + structured details, all in one place
        public async Task<IActionResult> Preview(int id)
        {
            var (_, authorized) = await AuthorizeResumeAsync(id);
            if (!authorized) return Forbid();

            var resume = await _context.Resumes
                .Include(r => r.Educations)
                .Include(r => r.Experiences)
                .Include(r => r.Certifications)
                .Include(r => r.Projects)
                .Include(r => r.ExtracurricularActivities)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (resume == null) return NotFound();

            ViewBag.IsPdf = resume.FilePath.EndsWith(".pdf");
            return View(resume);
        }
    }
}