using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AI_powerd_job_search_management_system.Data;
using AI_powerd_job_search_management_system.Models;
using AI_powerd_job_search_management_system.ViewModels;

namespace AI_powerd_job_search_management_system.Controllers
{
    [Authorize(Roles = "JobSeeker")]
    public class JobSeekerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public JobSeekerController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        private async Task<JobSeeker?> GetCurrentJobSeekerAsync()
        {
            var userId = _userManager.GetUserId(User);
            return await _context.JobSeekers.FirstOrDefaultAsync(js => js.ApplicationUserId == userId);
        }

        public async Task<IActionResult> Index(string? search, string? location)
        {
            var jobsQuery = _context.Jobs
                .Include(j => j.Employer)
                    .ThenInclude(e => e!.Company)
                    .Where(j => j.Status == JobStatus.Open && j.Employer!.Company!.IsApproved);

            if (!string.IsNullOrWhiteSpace(search))
                jobsQuery = jobsQuery.Where(j => j.Title.Contains(search));

            if (!string.IsNullOrWhiteSpace(location))
                jobsQuery = jobsQuery.Where(j => j.Location != null && j.Location.Contains(location));

            var jobs = await jobsQuery.OrderByDescending(j => j.PostedAt).ToListAsync();

            ViewBag.Search = search;
            ViewBag.Location = location;

            return View(jobs);
        }

        public async Task<IActionResult> Details(int id)
        {
            var job = await _context.Jobs
                .Include(j => j.Employer)
                    .ThenInclude(e => e!.Company)
                .Include(j => j.JobSkills)
                    .ThenInclude(js => js.Skill)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null) return NotFound();
            var jobSeeker = await GetCurrentJobSeekerAsync();
            ViewBag.AlreadyApplied = jobSeeker != null &&
                await _context.JobApplications.AnyAsync(a => a.JobId == id && a.JobSeekerId == jobSeeker.Id);
            return View(job);
        }

        [HttpGet]
        public IActionResult CompleteProfile()
        {
            return View(new CompleteJobSeekerProfileViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteProfile(CompleteJobSeekerProfileViewModel model)
        {
            var userId = _userManager.GetUserId(User);

            var existing = await _context.JobSeekers.FirstOrDefaultAsync(js => js.ApplicationUserId == userId);
            if (existing != null) return RedirectToAction("Index");

            var jobSeeker = new JobSeeker
            {
                ApplicationUserId = userId!,
                Bio = model.Bio,
                Location = model.Location
            };

            _context.JobSeekers.Add(jobSeeker);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> MyResumes()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();
            if (jobSeeker == null) return RedirectToAction("CompleteProfile");

            var resumes = await _context.Resumes
                .Where(r => r.JobSeekerId == jobSeeker.Id)
                .OrderByDescending(r => r.UploadedAt)
                .ToListAsync();

            return View(resumes);
        }
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();
            if (jobSeeker == null) return RedirectToAction("CompleteProfile");

            var user = await _userManager.GetUserAsync(User);
            ViewBag.FullName = user!.FullName;
            ViewBag.Email = user.Email;

            return View(jobSeeker);
        }

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();
            if (jobSeeker == null) return RedirectToAction("CompleteProfile");

            var model = new CompleteJobSeekerProfileViewModel
            {
                Bio = jobSeeker.Bio,
                Location = jobSeeker.Location
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(CompleteJobSeekerProfileViewModel model)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();
            if (jobSeeker == null) return RedirectToAction("CompleteProfile");

            jobSeeker.Bio = model.Bio;
            jobSeeker.Location = model.Location;

            await _context.SaveChangesAsync();

            return RedirectToAction("Profile");
        }

        [HttpGet]
        public IActionResult UploadResume()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadResume(ResumeUploadViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var allowedExtensions = new[] { ".pdf", ".docx" };
            var extension = Path.GetExtension(model.File.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("", "Only PDF or DOCX files are allowed.");
                return View(model);
            }

            if (model.File.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("", "File size must be under 5MB.");
                return View(model);
            }

            var jobSeeker = await GetCurrentJobSeekerAsync();
            if (jobSeeker == null) return RedirectToAction("CompleteProfile");

            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var uploadPath = Path.Combine(_env.ContentRootPath, "UploadedFiles", "Resumes");
            Directory.CreateDirectory(uploadPath);
            var fullPath = Path.Combine(uploadPath, uniqueFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await model.File.CopyToAsync(stream);
            }

            var resume = new Resume
            {
                JobSeekerId = jobSeeker.Id,
                FilePath = uniqueFileName,
                OriginalFileName = model.File.FileName,
                IsActive = true
            };

            _context.Resumes.Add(resume);
            await _context.SaveChangesAsync();

            return RedirectToAction("MyResumes");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteResume(int id)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();
            var resume = await _context.Resumes.FirstOrDefaultAsync(r => r.Id == id && r.JobSeekerId == jobSeeker!.Id);
            if (resume == null) return NotFound();

            var fullPath = Path.Combine(_env.ContentRootPath, "UploadedFiles", "Resumes", resume.FilePath);
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);

            _context.Resumes.Remove(resume);
            await _context.SaveChangesAsync();

            return RedirectToAction("MyResumes");
        }
        [HttpGet]
        public async Task<IActionResult> Apply(int jobId)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();
            if (jobSeeker == null) return RedirectToAction("CompleteProfile");

            var job = await _context.Jobs
                .Include(j => j.Employer)
                    .ThenInclude(e => e!.Company)
                .FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) return NotFound();

            var alreadyApplied = await _context.JobApplications
                .AnyAsync(a => a.JobId == jobId && a.JobSeekerId == jobSeeker.Id);
            if (alreadyApplied) return RedirectToAction("MyApplications");

            var resumes = await _context.Resumes
                .Where(r => r.JobSeekerId == jobSeeker.Id)
                .OrderByDescending(r => r.UploadedAt)
                .ToListAsync();

            if (!resumes.Any())
            {
                TempData["Message"] = "Upload a resume before applying.";
                return RedirectToAction("UploadResume");
            }

            ViewBag.Job = job;
            ViewBag.Resumes = resumes;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(int jobId, int resumeId)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();
            if (jobSeeker == null) return RedirectToAction("CompleteProfile");

            var alreadyApplied = await _context.JobApplications
                .AnyAsync(a => a.JobId == jobId && a.JobSeekerId == jobSeeker.Id);
            if (alreadyApplied) return RedirectToAction("MyApplications");

            var resume = await _context.Resumes
                .FirstOrDefaultAsync(r => r.Id == resumeId && r.JobSeekerId == jobSeeker.Id);
            if (resume == null) return NotFound();

            var jobExists = await _context.Jobs.AnyAsync(j => j.Id == jobId);
            if (!jobExists) return NotFound();

            var application = new JobApplication
            {
                JobId = jobId,
                JobSeekerId = jobSeeker.Id,
                ResumeId = resumeId,
                Status = ApplicationStatus.Pending,
                MatchScore = 0
            };

            _context.JobApplications.Add(application);
            await _context.SaveChangesAsync();

            return RedirectToAction("MyApplications");
        }

        [HttpGet]
        public async Task<IActionResult> MyApplications()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();
            if (jobSeeker == null) return RedirectToAction("CompleteProfile");

            var applications = await _context.JobApplications
                .Include(a => a.Job)
                    .ThenInclude(j => j!.Employer)
                        .ThenInclude(e => e!.Company)
                .Where(a => a.JobSeekerId == jobSeeker.Id)
                .OrderByDescending(a => a.AppliedAt)
                .ToListAsync();

            return View(applications);
        }
    }
}