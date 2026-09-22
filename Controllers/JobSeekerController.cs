using AI_powerd_job_search_management_system.Data;
using AI_powerd_job_search_management_system.Models;
using AI_powerd_job_search_management_system.Services;
using AI_powerd_job_search_management_system.Utilities;
using AI_powerd_job_search_management_system.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AI_powerd_job_search_management_system.Controllers
{
    [Authorize(Roles = "JobSeeker")]
    public class JobSeekerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly GeminiService _gemini;

        public JobSeekerController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            GeminiService gemini)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _gemini = gemini;
        }

        // CURRENT JOB SEEKER

        private async Task<JobSeeker?> GetCurrentJobSeekerAsync()
        {
            var userId = _userManager.GetUserId(User);

            return await _context.JobSeekers
                .FirstOrDefaultAsync(js =>
                    js.ApplicationUserId == userId);
        }

        // BROWSE JOBS

        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            string? location,
            string? category)
        {
            var jobsQuery = _context.Jobs
                .Include(j => j.Employer)
                    .ThenInclude(e => e!.Company)
                .Where(j =>
                    j.Status == JobStatus.Open &&
                    j.Employer!.Company!.IsApproved);

            if (!string.IsNullOrWhiteSpace(search))
            {
                jobsQuery = jobsQuery.Where(j =>
                    j.Title.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                jobsQuery = jobsQuery.Where(j =>
                    j.Location != null &&
                    j.Location.Contains(location));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                jobsQuery = jobsQuery.Where(j =>
                    j.Category == category);
            }

            var jobs = await jobsQuery
                .OrderByDescending(j => j.PostedAt)
                .ToListAsync();

            var rawCounts = await _context.Jobs
                .Where(j =>
                    j.Status == JobStatus.Open &&
                    j.Employer!.Company!.IsApproved)
                .GroupBy(j => j.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.Category, x => x.Count);

            var categoryCounts = JobCategories.All
                .ToDictionary(
                    c => c,
                    c => rawCounts.ContainsKey(c)
                        ? rawCounts[c]
                        : 0);

            ViewBag.Search = search;
            ViewBag.Location = location;
            ViewBag.Category = category;
            ViewBag.CategoryCounts = categoryCounts;

            if (!string.IsNullOrWhiteSpace(category))
            {
                var categoryJobIds = await _context.Jobs
                    .Where(j =>
                        j.Status == JobStatus.Open &&
                        j.Employer!.Company!.IsApproved &&
                        j.Category == category)
                    .Select(j => j.Id)
                    .ToListAsync();

                int totalJobsInCategory = categoryJobIds.Count;

                var skillDemand = await _context.JobSkills
                    .Where(js => categoryJobIds.Contains(js.JobId))
                    .Include(js => js.Skill)
                    .GroupBy(js => js.Skill!.Name)
                    .Select(g => new
                    {
                        Skill = g.Key,
                        JobCount = g.Count()
                    })
                    .OrderByDescending(g => g.JobCount)
                    .Take(6)
                    .ToListAsync();

                ViewBag.SkillLabels = skillDemand
                    .Select(s => s.Skill)
                    .ToList();

                ViewBag.SkillPercents = skillDemand
                    .Select(s => totalJobsInCategory > 0
                        ? Math.Round(
                            (double)s.JobCount /
                            totalJobsInCategory * 100, 1)
                        : 0)
                    .ToList();

                ViewBag.TotalJobsInCategory = totalJobsInCategory;
            }

            return View(jobs);
        }

        // JOB DETAILS

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var job = await _context.Jobs
                .Include(j => j.Employer)
                    .ThenInclude(e => e!.Company)
                .Include(j => j.JobSkills)
                    .ThenInclude(js => js.Skill)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null)
                return NotFound();

            var jobSeeker = await GetCurrentJobSeekerAsync();

            ViewBag.AlreadyApplied =
                jobSeeker != null &&
                await _context.JobApplications.AnyAsync(a =>
                    a.JobId == id &&
                    a.JobSeekerId == jobSeeker.Id);

            ViewBag.IsSaved =
                jobSeeker != null &&
                await _context.SavedJobs.AnyAsync(s =>
                    s.JobId == id &&
                    s.JobSeekerId == jobSeeker.Id);

            return View(job);
        }

        // COMPLETE PROFILE

        [HttpGet]
        public async Task<IActionResult> CompleteProfile()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker != null)
                return RedirectToAction(nameof(EditProfile));

            return View(new CompleteJobSeekerProfileViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteProfile(
            CompleteJobSeekerProfileViewModel model)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            if (!ModelState.IsValid)
                return View(model);

            var jobSeeker = await _context.JobSeekers
                .FirstOrDefaultAsync(js =>
                    js.ApplicationUserId == userId);

            if (jobSeeker == null)
            {
                jobSeeker = new JobSeeker
                {
                    ApplicationUserId = userId
                };

                _context.JobSeekers.Add(jobSeeker);
            }

            jobSeeker.Bio = model.Bio;
            jobSeeker.Location = model.Location;

            await _context.SaveChangesAsync();

            return await OpenProfileResumeDetailsAsync(jobSeeker.Id);
        }

        // MY PROFILE

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = _userManager.GetUserId(User);

            var jobSeeker = await _context.JobSeekers
                .Include(js => js.Skills)
                    .ThenInclude(s => s.Skill)
                .FirstOrDefaultAsync(js =>
                    js.ApplicationUserId == userId);

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            ViewBag.FullName = user.FullName;
            ViewBag.Email = user.Email;

            var activeResume = await ProfileResumeQuery(jobSeeker.Id)
                .Include(r => r.Educations)
                .Include(r => r.Experiences)
                .Include(r => r.Certifications)
                .Include(r => r.Projects)
                .Include(r => r.ExtracurricularActivities)
                .FirstOrDefaultAsync();

            ViewBag.ActiveResume = activeResume;

            return View(jobSeeker);
        }

        // EDIT PROFILE

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            return View(new CompleteJobSeekerProfileViewModel
            {
                Bio = jobSeeker.Bio,
                Location = jobSeeker.Location
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(
            CompleteJobSeekerProfileViewModel model,
            string? next)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            if (!ModelState.IsValid)
                return View(model);

            jobSeeker.Bio = model.Bio;
            jobSeeker.Location = model.Location;

            await _context.SaveChangesAsync();

            if (next == "resumeDetails")
            {
                return await OpenProfileResumeDetailsAsync(jobSeeker.Id);
            }

            return RedirectToAction(nameof(Profile));
        }

        // RESUME SELECTION HELPERS

        private IOrderedQueryable<Resume> ProfileResumeQuery(
            int jobSeekerId)
        {
            // Prefer records with saved qualifications.
            // The same rule is used by Profile and EditProfile.
            return _context.Resumes
                .Where(r => r.JobSeekerId == jobSeekerId)
                .OrderByDescending(r =>
                    r.Educations.Any() ||
                    r.Experiences.Any() ||
                    r.Certifications.Any() ||
                    r.Projects.Any() ||
                    r.ExtracurricularActivities.Any())
                .ThenByDescending(r => r.FilePath == "")
                .ThenByDescending(r => r.IsActive)
                .ThenByDescending(r => r.UploadedAt)
                .ThenByDescending(r => r.Id);
        }

        private async Task<IActionResult> OpenProfileResumeDetailsAsync(
            int jobSeekerId)
        {
            var resume = await ProfileResumeQuery(jobSeekerId)
                .FirstOrDefaultAsync();

            if (resume == null)
            {
                resume = new Resume
                {
                    JobSeekerId = jobSeekerId,
                    FilePath = "",
                    OriginalFileName = "",
                    IsActive = false
                };

                _context.Resumes.Add(resume);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(
                nameof(ResumeDetails),
                new { resumeId = resume.Id });
        }

        private async Task<Resume?> GetOwnedResumeAsync(int resumeId)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return null;

            return await _context.Resumes
                .FirstOrDefaultAsync(r =>
                    r.Id == resumeId &&
                    r.JobSeekerId == jobSeeker.Id);
        }

        // RESUME DETAILS

        [HttpGet]
        public async Task<IActionResult> ResumeDetails(int resumeId)
        {
            var resume = await GetOwnedResumeAsync(resumeId);

            if (resume == null)
                return NotFound();

            return await RenderResumeDetailsAsync(resume);
        }

        private async Task<IActionResult> RenderResumeDetailsAsync(
            Resume resume,
            object? attempted = null)
        {
            var educations = await _context.Educations
                .AsNoTracking()
                .Where(e => e.ResumeId == resume.Id)
                .OrderByDescending(e => e.StartDate)
                .ToListAsync();

            var experiences = await _context.Experiences
                .AsNoTracking()
                .Where(e => e.ResumeId == resume.Id)
                .OrderByDescending(e => e.StartDate)
                .ToListAsync();

            var certifications = await _context.Certifications
                .AsNoTracking()
                .Where(c => c.ResumeId == resume.Id)
                .OrderByDescending(c => c.DateIssued)
                .ToListAsync();

            var projects = await _context.Projects
                .AsNoTracking()
                .Where(p => p.ResumeId == resume.Id)
                .ToListAsync();

            var activities = await _context.ExtracurricularActivities
                .AsNoTracking()
                .Where(a => a.ResumeId == resume.Id)
                .ToListAsync();

            // Preserve submitted values after an invalid update.
            if (attempted is Education education)
            {
                educations = educations
                    .Select(e => e.Id == education.Id ? education : e)
                    .ToList();
            }

            if (attempted is Experience experience)
            {
                experiences = experiences
                    .Select(e => e.Id == experience.Id ? experience : e)
                    .ToList();
            }

            if (attempted is Certification certificate)
            {
                certifications = certifications
                    .Select(c => c.Id == certificate.Id ? certificate : c)
                    .ToList();
            }

            if (attempted is Project project)
            {
                projects = projects
                    .Select(p => p.Id == project.Id ? project : p)
                    .ToList();
            }

            if (attempted is ExtracurricularActivity activity)
            {
                activities = activities
                    .Select(a => a.Id == activity.Id ? activity : a)
                    .ToList();
            }

            ViewBag.Resume = resume;
            ViewBag.Educations = educations;
            ViewBag.Experiences = experiences;
            ViewBag.Certifications = certifications;
            ViewBag.Projects = projects;
            ViewBag.Activities = activities;

            var records = await _context.Resumes
                .AsNoTracking()
                .Where(r => r.JobSeekerId == resume.JobSeekerId)
                .OrderByDescending(r => r.Id)
                .Select(r => new
                {
                    r.Id,
                    r.FilePath,
                    EducationCount = r.Educations.Count(),
                    ExperienceCount = r.Experiences.Count(),
                    CertificateCount = r.Certifications.Count(),
                    ProjectCount = r.Projects.Count(),
                    ActivityCount = r.ExtracurricularActivities.Count()
                })
                .ToListAsync();

            ViewBag.ResumeChoices = records
                .Select(r => new SelectListItem
                {
                    Value = r.Id.ToString(),
                    Text =
                        $"Resume {r.Id}" +
                        (string.IsNullOrWhiteSpace(r.FilePath)
                            ? " — awaiting upload"
                            : " — uploaded") +
                        $" | Education: {r.EducationCount}" +
                        $" | Experience: {r.ExperienceCount}" +
                        $" | Certificates: {r.CertificateCount}" +
                        $" | Projects: {r.ProjectCount}" +
                        $" | Activities: {r.ActivityCount}",
                    Selected = r.Id == resume.Id
                })
                .ToList();

            return View("ResumeDetails");
        }

        [HttpGet]
        public async Task<IActionResult> FinishResumeDetails(int resumeId)
        {
            var resume = await GetOwnedResumeAsync(resumeId);

            if (resume == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(resume.FilePath))
            {
                return RedirectToAction(
                    nameof(UploadResume),
                    new { resumeId = resume.Id });
            }

            return RedirectToAction(nameof(Profile));
        }

        // UPLOAD RESUME

        [HttpGet]
        public async Task<IActionResult> UploadResume(int? resumeId)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            Resume? resume;

            if (resumeId.HasValue)
            {
                resume = await _context.Resumes
                    .FirstOrDefaultAsync(r =>
                        r.Id == resumeId.Value &&
                        r.JobSeekerId == jobSeeker.Id);

                if (resume == null)
                    return NotFound();

                if (!string.IsNullOrWhiteSpace(resume.FilePath))
                    return RedirectToAction(nameof(MyResumes));
            }
            else
            {
                resume = await _context.Resumes
                    .Where(r =>
                        r.JobSeekerId == jobSeeker.Id &&
                        r.FilePath == "")
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefaultAsync();
            }

            return View(new ResumeUploadViewModel
            {
                ResumeId = resume?.Id
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadResume(
            ResumeUploadViewModel model)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            Resume? resume;

            if (model.ResumeId.HasValue)
            {
                resume = await _context.Resumes
                    .FirstOrDefaultAsync(r =>
                        r.Id == model.ResumeId.Value &&
                        r.JobSeekerId == jobSeeker.Id);

                if (resume == null)
                    return NotFound();

                if (!string.IsNullOrWhiteSpace(resume.FilePath))
                {
                    return BadRequest(
                        "This resume already has an uploaded file.");
                }
            }
            else
            {
                resume = await _context.Resumes
                    .Where(r =>
                        r.JobSeekerId == jobSeeker.Id &&
                        r.FilePath == "")
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefaultAsync();
            }

            if (!ModelState.IsValid)
                return View(model);

            if (model.File == null || model.File.Length == 0)
            {
                ModelState.AddModelError(
                    nameof(model.File),
                    "Please select a non-empty resume file.");

                return View(model);
            }

            var extension = Path.GetExtension(model.File.FileName)
                .ToLowerInvariant();

            if (extension != ".pdf" && extension != ".docx")
            {
                ModelState.AddModelError(
                    nameof(model.File),
                    "Only PDF or DOCX files are allowed.");

                return View(model);
            }

            if (model.File.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(
                    nameof(model.File),
                    "File size must not exceed 5 MB.");

                return View(model);
            }

            var uploadDirectory = Path.Combine(
                _env.ContentRootPath,
                "UploadedFiles",
                "Resumes");

            Directory.CreateDirectory(uploadDirectory);

            var storedFileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(uploadDirectory, storedFileName);

            try
            {
                await using (var stream =
                    new FileStream(fullPath, FileMode.CreateNew))
                {
                    await model.File.CopyToAsync(stream);
                }

                if (resume == null)
                {
                    resume = new Resume
                    {
                        JobSeekerId = jobSeeker.Id
                    };

                    _context.Resumes.Add(resume);
                }

                // Update the same draft so its details remain linked.
                resume.FilePath = storedFileName;
                resume.OriginalFileName =
                    Path.GetFileName(model.File.FileName);
                resume.UploadedAt = DateTime.UtcNow;
                resume.IsActive = true;

                await _context.SaveChangesAsync();
            }
            catch
            {
                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);

                throw;
            }

            return RedirectToAction(nameof(Profile));
        }

        // MY RESUMES

        [HttpGet]
        public async Task<IActionResult> MyResumes()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            var resumes = await _context.Resumes
                .Where(r =>
                    r.JobSeekerId == jobSeeker.Id &&
                    r.FilePath != "")
                .OrderByDescending(r => r.UploadedAt)
                .ToListAsync();

            return View(resumes);
        }

        // DELETE RESUME

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteResume(int id)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            var resume = await _context.Resumes
                .FirstOrDefaultAsync(r =>
                    r.Id == id &&
                    r.JobSeekerId == jobSeeker.Id);

            if (resume == null)
                return NotFound();

            // Do not delete a file used by an existing application.
            var isUsed = await _context.JobApplications
                .AnyAsync(a => a.ResumeId == resume.Id);

            if (isUsed)
            {
                return BadRequest(
                    "This resume is used by a job application and cannot be deleted.");
            }

            var storedFileName = resume.FilePath;

            _context.Resumes.Remove(resume);

            // Delete the database record before removing the file.
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(storedFileName))
            {
                var fullPath = Path.Combine(
                    _env.ContentRootPath,
                    "UploadedFiles",
                    "Resumes",
                    storedFileName);

                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
            }

            return RedirectToAction(nameof(MyResumes));
        }

        // EDUCATION: ADD

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEducation(
            int resumeId,
            string institution,
            string? degree,
            string? fieldOfStudy,
            DateTime? startDate,
            DateTime? endDate)
        {
            var resume = await GetOwnedResumeAsync(resumeId);

            if (resume == null)
                return NotFound();

            institution = institution?.Trim() ?? "";
            degree = degree?.Trim();
            fieldOfStudy = fieldOfStudy?.Trim();

            if (!ModelState.IsValid ||
                string.IsNullOrWhiteSpace(institution) ||
                institution.Length > 200 ||
                (degree?.Length ?? 0) > 150 ||
                (fieldOfStudy?.Length ?? 0) > 150)
            {
                TempData["ResumeError"] =
                    "Check the education fields. Institution is required " +
                    "and must be within 200 characters. Degree and field " +
                    "of study must each be within 150 characters.";

                return RedirectToAction(
                    nameof(ResumeDetails), new { resumeId });
            }

            if (startDate.HasValue &&
                endDate.HasValue &&
                endDate.Value.Date < startDate.Value.Date)
            {
                TempData["ResumeError"] =
                    "Education end date cannot be earlier than start date.";

                return RedirectToAction(
                    nameof(ResumeDetails), new { resumeId });
            }

            _context.Educations.Add(new Education
            {
                ResumeId = resumeId,
                Institution = institution,
                Degree = degree,
                FieldOfStudy = fieldOfStudy,
                StartDate = startDate?.Date,
                EndDate = endDate?.Date
            });

            await _context.SaveChangesAsync();
            TempData["ResumeSuccess"] = "Education added.";

            return RedirectToAction(
                nameof(ResumeDetails), new { resumeId });
        }

        // EDUCATION: UPDATE

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEducation(
            [Bind("Id,ResumeId,Institution,Degree,FieldOfStudy,StartDate,EndDate")]
            Education model)
        {
            var resume = await GetOwnedResumeAsync(model.ResumeId);

            if (resume == null)
                return NotFound();

            var item = await _context.Educations
                .FirstOrDefaultAsync(e =>
                    e.Id == model.Id &&
                    e.ResumeId == resume.Id);

            if (item == null)
                return NotFound();

            if (model.StartDate.HasValue &&
                model.EndDate.HasValue &&
                model.EndDate.Value < model.StartDate.Value)
            {
                ModelState.AddModelError(
                    "EndDate",
                    "End date cannot be earlier than start date.");
            }

            if (!ModelState.IsValid)
                return await RenderResumeDetailsAsync(resume, model);

            item.Institution = model.Institution.Trim();
            item.Degree = model.Degree?.Trim();
            item.FieldOfStudy = model.FieldOfStudy?.Trim();
            item.StartDate = model.StartDate;
            item.EndDate = model.EndDate;

            await _context.SaveChangesAsync();
            TempData["ResumeSuccess"] = "Education updated.";

            return RedirectToAction(
                nameof(ResumeDetails),
                new { resumeId = resume.Id });
        }

        // EDUCATION: DELETE

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEducation(
            int id,
            int resumeId)
        {
            var resume = await GetOwnedResumeAsync(resumeId);

            if (resume == null)
                return NotFound();

            var item = await _context.Educations
                .FirstOrDefaultAsync(e =>
                    e.Id == id && e.ResumeId == resumeId);

            if (item != null)
            {
                _context.Educations.Remove(item);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(
                nameof(ResumeDetails), new { resumeId });
        }

        // EXPERIENCE: ADD

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddExperience(
            int resumeId,
            string companyName,
            string jobTitle,
            DateTime? startDate,
            DateTime? endDate,
            string? description)
        {
            var resume = await GetOwnedResumeAsync(resumeId);

            if (resume == null)
                return NotFound();

            companyName = companyName?.Trim() ?? "";
            jobTitle = jobTitle?.Trim() ?? "";

            if (!ModelState.IsValid ||
                string.IsNullOrWhiteSpace(companyName) ||
                string.IsNullOrWhiteSpace(jobTitle) ||
                companyName.Length > 150 ||
                jobTitle.Length > 150)
            {
                TempData["ResumeError"] =
                    "Company name and job title are required and " +
                    "must each be within 150 characters. Check the dates too.";

                return RedirectToAction(
                    nameof(ResumeDetails), new { resumeId });
            }

            if (startDate.HasValue &&
                endDate.HasValue &&
                endDate.Value.Date < startDate.Value.Date)
            {
                TempData["ResumeError"] =
                    "Experience end date cannot be earlier than start date.";

                return RedirectToAction(
                    nameof(ResumeDetails), new { resumeId });
            }

            _context.Experiences.Add(new Experience
            {
                ResumeId = resumeId,
                CompanyName = companyName,
                JobTitle = jobTitle,
                StartDate = startDate?.Date,
                EndDate = endDate?.Date,
                Description = description?.Trim()
            });

            await _context.SaveChangesAsync();
            TempData["ResumeSuccess"] = "Experience added.";

            return RedirectToAction(
                nameof(ResumeDetails), new { resumeId });
        }

        // EXPERIENCE: UPDATE

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateExperience(
            [Bind("Id,ResumeId,CompanyName,JobTitle,StartDate,EndDate,Description")]
            Experience model)
        {
            var resume = await GetOwnedResumeAsync(model.ResumeId);

            if (resume == null)
                return NotFound();

            var item = await _context.Experiences
                .FirstOrDefaultAsync(e =>
                    e.Id == model.Id &&
                    e.ResumeId == resume.Id);

            if (item == null)
                return NotFound();

            if (model.StartDate.HasValue &&
                model.EndDate.HasValue &&
                model.EndDate.Value < model.StartDate.Value)
            {
                ModelState.AddModelError(
                    "EndDate",
                    "End date cannot be earlier than start date.");
            }

            if (!ModelState.IsValid)
                return await RenderResumeDetailsAsync(resume, model);

            item.CompanyName = model.CompanyName.Trim();
            item.JobTitle = model.JobTitle.Trim();
            item.StartDate = model.StartDate;
            item.EndDate = model.EndDate;
            item.Description = model.Description?.Trim();

            await _context.SaveChangesAsync();
            TempData["ResumeSuccess"] = "Experience updated.";

            return RedirectToAction(
                nameof(ResumeDetails),
                new { resumeId = resume.Id });
        }

        // EXPERIENCE: DELETE

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteExperience(
            int id,
            int resumeId)
        {
            var resume = await GetOwnedResumeAsync(resumeId);

            if (resume == null)
                return NotFound();

            var item = await _context.Experiences
                .FirstOrDefaultAsync(e =>
                    e.Id == id && e.ResumeId == resumeId);

            if (item != null)
            {
                _context.Experiences.Remove(item);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(
                nameof(ResumeDetails), new { resumeId });
        }

        // CERTIFICATION: ADD

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCertification(
            int resumeId,
            string name,
            string? issuingOrganization,
            DateTime? dateIssued)
        {
            var resume = await GetOwnedResumeAsync(resumeId);

            if (resume == null)
                return NotFound();

            name = name?.Trim() ?? "";
            issuingOrganization = issuingOrganization?.Trim();

            if (!ModelState.IsValid ||
                string.IsNullOrWhiteSpace(name) ||
                name.Length > 200 ||
                (issuingOrganization?.Length ?? 0) > 200)
            {
                TempData["ResumeError"] =
                    "Check the certification fields. Name is required. " +
                    "Name and issuing organization must be within 200 characters.";

                return RedirectToAction(
                    nameof(ResumeDetails), new { resumeId });
            }

            _context.Certifications.Add(new Certification
            {
                ResumeId = resumeId,
                Name = name,
                IssuingOrganization = issuingOrganization,
                DateIssued = dateIssued
            });

            await _context.SaveChangesAsync();
            TempData["ResumeSuccess"] = "Certification added.";

            return RedirectToAction(
                nameof(ResumeDetails), new { resumeId });
        }

        // CERTIFICATION: UPDATE

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCertification(
            [Bind("Id,ResumeId,Name,IssuingOrganization,DateIssued")]
            Certification model)
        {
            var resume = await GetOwnedResumeAsync(model.ResumeId);

            if (resume == null)
                return NotFound();

            var item = await _context.Certifications
                .FirstOrDefaultAsync(c =>
                    c.Id == model.Id &&
                    c.ResumeId == resume.Id);

            if (item == null)
                return NotFound();

            if (!ModelState.IsValid)
                return await RenderResumeDetailsAsync(resume, model);

            item.Name = model.Name.Trim();
            item.IssuingOrganization =
                model.IssuingOrganization?.Trim();
            item.DateIssued = model.DateIssued;

            await _context.SaveChangesAsync();
            TempData["ResumeSuccess"] = "Certification updated.";

            return RedirectToAction(
                nameof(ResumeDetails),
                new { resumeId = resume.Id });
        }

        // CERTIFICATION: DELETE

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCertification(
            int id,
            int resumeId)
        {
            var resume = await GetOwnedResumeAsync(resumeId);

            if (resume == null)
                return NotFound();

            var item = await _context.Certifications
                .FirstOrDefaultAsync(c =>
                    c.Id == id && c.ResumeId == resumeId);

            if (item != null)
            {
                _context.Certifications.Remove(item);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(
                nameof(ResumeDetails), new { resumeId });
        }

        // PROJECT: ADD

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddProject(
            int resumeId,
            string title,
            string? description,
            string? technologiesUsed)
        {
            var resume = await GetOwnedResumeAsync(resumeId);

            if (resume == null)
                return NotFound();

            title = title?.Trim() ?? "";

            if (!ModelState.IsValid ||
                string.IsNullOrWhiteSpace(title) ||
                title.Length > 200)
            {
                TempData["ResumeError"] =
                    "Project title is required and must be within 200 characters.";

                return RedirectToAction(
                    nameof(ResumeDetails), new { resumeId });
            }

            _context.Projects.Add(new Project
            {
                ResumeId = resumeId,
                Title = title,
                Description = description?.Trim(),
                TechnologiesUsed = technologiesUsed?.Trim()
            });

            await _context.SaveChangesAsync();
            TempData["ResumeSuccess"] = "Project added.";

            return RedirectToAction(
                nameof(ResumeDetails), new { resumeId });
        }

        // PROJECT: UPDATE

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProject(
            [Bind("Id,ResumeId,Title,Description,TechnologiesUsed")]
            Project model)
        {
            var resume = await GetOwnedResumeAsync(model.ResumeId);

            if (resume == null)
                return NotFound();

            var item = await _context.Projects
                .FirstOrDefaultAsync(p =>
                    p.Id == model.Id &&
                    p.ResumeId == resume.Id);

            if (item == null)
                return NotFound();

            if (!ModelState.IsValid)
                return await RenderResumeDetailsAsync(resume, model);

            item.Title = model.Title.Trim();
            item.Description = model.Description?.Trim();
            item.TechnologiesUsed = model.TechnologiesUsed?.Trim();

            await _context.SaveChangesAsync();
            TempData["ResumeSuccess"] = "Project updated.";

            return RedirectToAction(
                nameof(ResumeDetails),
                new { resumeId = resume.Id });
        }

        // PROJECT: DELETE

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProject(
            int id,
            int resumeId)
        {
            var resume = await GetOwnedResumeAsync(resumeId);

            if (resume == null)
                return NotFound();

            var item = await _context.Projects
                .FirstOrDefaultAsync(p =>
                    p.Id == id && p.ResumeId == resumeId);

            if (item != null)
            {
                _context.Projects.Remove(item);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(
                nameof(ResumeDetails), new { resumeId });
        }

        // ACTIVITY: ADD

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddActivity(
            int resumeId,
            string activityName,
            string? roleOrPosition,
            string? description)
        {
            var resume = await GetOwnedResumeAsync(resumeId);

            if (resume == null)
                return NotFound();

            activityName = activityName?.Trim() ?? "";

            if (!ModelState.IsValid ||
                string.IsNullOrWhiteSpace(activityName) ||
                activityName.Length > 200)
            {
                TempData["ResumeError"] =
                    "Activity name is required and must be within 200 characters.";

                return RedirectToAction(
                    nameof(ResumeDetails), new { resumeId });
            }

            _context.ExtracurricularActivities.Add(
                new ExtracurricularActivity
                {
                    ResumeId = resumeId,
                    ActivityName = activityName,
                    RoleOrPosition = roleOrPosition?.Trim(),
                    Description = description?.Trim()
                });

            await _context.SaveChangesAsync();
            TempData["ResumeSuccess"] = "Activity added.";

            return RedirectToAction(
                nameof(ResumeDetails), new { resumeId });
        }

        // ACTIVITY: UPDATE

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateActivity(
            [Bind("Id,ResumeId,ActivityName,RoleOrPosition,Description")]
            ExtracurricularActivity model)
        {
            var resume = await GetOwnedResumeAsync(model.ResumeId);

            if (resume == null)
                return NotFound();

            var item = await _context.ExtracurricularActivities
                .FirstOrDefaultAsync(a =>
                    a.Id == model.Id &&
                    a.ResumeId == resume.Id);

            if (item == null)
                return NotFound();

            if (!ModelState.IsValid)
                return await RenderResumeDetailsAsync(resume, model);

            item.ActivityName = model.ActivityName.Trim();
            item.RoleOrPosition = model.RoleOrPosition?.Trim();
            item.Description = model.Description?.Trim();

            await _context.SaveChangesAsync();
            TempData["ResumeSuccess"] = "Activity updated.";

            return RedirectToAction(
                nameof(ResumeDetails),
                new { resumeId = resume.Id });
        }

        // ACTIVITY: DELETE

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteActivity(
            int id,
            int resumeId)
        {
            var resume = await GetOwnedResumeAsync(resumeId);

            if (resume == null)
                return NotFound();

            var item = await _context.ExtracurricularActivities
                .FirstOrDefaultAsync(a =>
                    a.Id == id && a.ResumeId == resumeId);

            if (item != null)
            {
                _context.ExtracurricularActivities.Remove(item);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(
                nameof(ResumeDetails), new { resumeId });
        }

        // MATCH SCORE AND GEMINI INSIGHT

        private async Task RecalculateMatchAsync(
            JobApplication application)
        {
            var job = await _context.Jobs
                .FirstOrDefaultAsync(j =>
                    j.Id == application.JobId);

            var requiredSkills = await _context.JobSkills
                .Where(js => js.JobId == application.JobId)
                .Include(js => js.Skill)
                .Select(js => js.Skill!.Name.Trim())
                .ToListAsync();

            var candidateSkills = await _context.JobSeekerSkills
                .Where(s =>
                    s.JobSeekerId == application.JobSeekerId)
                .Include(s => s.Skill)
                .Select(s => s.Skill!.Name.Trim())
                .ToListAsync();

            var matchedSkills = requiredSkills
                .Where(r => candidateSkills.Any(c =>
                    SkillMatcher.IsMatch(c, r)))
                .ToList();

            var missingSkills = requiredSkills
                .Where(r => !candidateSkills.Any(c =>
                    SkillMatcher.IsMatch(c, r)))
                .ToList();

            double score = requiredSkills.Any()
                ? Math.Round(
                    (double)matchedSkills.Count /
                    requiredSkills.Count * 100, 1)
                : 0;

            application.MatchScore = score;

            var analysis = await _context.AIAnalyses
                .FirstOrDefaultAsync(a =>
                    a.JobApplicationId == application.Id);

            if (analysis == null)
            {
                analysis = new AIAnalysis
                {
                    JobApplicationId = application.Id
                };

                _context.AIAnalyses.Add(analysis);
            }

            analysis.MatchedSkills = matchedSkills.Any()
                ? string.Join(", ", matchedSkills)
                : "None";

            analysis.MissingSkills = missingSkills.Any()
                ? string.Join(", ", missingSkills)
                : "None";

            analysis.SkillMatchScore = score;
            analysis.OverallScore = score;

            if (job != null)
            {
                analysis.AIInsight =
                    await _gemini.GenerateMatchInsightAsync(
                        job.Title,
                        job.Description,
                        requiredSkills,
                        candidateSkills,
                        score);
            }

            await _context.SaveChangesAsync();
        }

        // APPLY FOR JOB

        [HttpGet]
        public async Task<IActionResult> Apply(int jobId)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            var job = await _context.Jobs
                .Include(j => j.Employer)
                    .ThenInclude(e => e!.Company)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null)
                return NotFound();

            var alreadyApplied = await _context.JobApplications
                .AnyAsync(a =>
                    a.JobId == jobId &&
                    a.JobSeekerId == jobSeeker.Id);

            if (alreadyApplied)
                return RedirectToAction(nameof(MyApplications));

            var resumes = await _context.Resumes
                .Where(r =>
                    r.JobSeekerId == jobSeeker.Id &&
                    r.FilePath != "")
                .OrderByDescending(r => r.UploadedAt)
                .ToListAsync();

            if (!resumes.Any())
            {
                TempData["Message"] =
                    "Upload a resume before applying.";

                return RedirectToAction(nameof(UploadResume));
            }

            ViewBag.Job = job;
            ViewBag.Resumes = resumes;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(
            int jobId,
            int resumeId)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            var alreadyApplied = await _context.JobApplications
                .AnyAsync(a =>
                    a.JobId == jobId &&
                    a.JobSeekerId == jobSeeker.Id);

            if (alreadyApplied)
                return RedirectToAction(nameof(MyApplications));

            var resume = await _context.Resumes
                .FirstOrDefaultAsync(r =>
                    r.Id == resumeId &&
                    r.JobSeekerId == jobSeeker.Id &&
                    r.FilePath != "");

            if (resume == null)
                return NotFound();

            var jobExists = await _context.Jobs
                .AnyAsync(j => j.Id == jobId);

            if (!jobExists)
                return NotFound();

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

            await RecalculateMatchAsync(application);

            return RedirectToAction(nameof(MyApplications));
        }

        // MY APPLICATIONS

        [HttpGet]
        public async Task<IActionResult> MyApplications()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            var applications = await _context.JobApplications
                .Include(a => a.Job)
                    .ThenInclude(j => j!.Employer)
                        .ThenInclude(e => e!.Company)
                .Include(a => a.AIAnalysis)
                .Where(a => a.JobSeekerId == jobSeeker.Id)
                .OrderByDescending(a => a.AppliedAt)
                .ToListAsync();

            return View(applications);
        }

        // EDIT APPLICATION

        [HttpGet]
        public async Task<IActionResult> EditApplication(int id)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            var application = await _context.JobApplications
                .FirstOrDefaultAsync(a =>
                    a.Id == id &&
                    a.JobSeekerId == jobSeeker.Id);

            if (application == null)
                return NotFound();

            if ((DateTime.UtcNow - application.AppliedAt).TotalHours > 24)
            {
                TempData["Message"] =
                    "The 24-hour edit window for this application has passed.";

                return RedirectToAction(nameof(MyApplications));
            }

            var resumes = await _context.Resumes
                .Where(r =>
                    r.JobSeekerId == jobSeeker.Id &&
                    r.FilePath != "")
                .OrderByDescending(r => r.UploadedAt)
                .ToListAsync();

            ViewBag.Application = application;
            ViewBag.Resumes = resumes;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditApplication(
            int id,
            int resumeId)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            var application = await _context.JobApplications
                .FirstOrDefaultAsync(a =>
                    a.Id == id &&
                    a.JobSeekerId == jobSeeker.Id);

            if (application == null)
                return NotFound();

            if ((DateTime.UtcNow - application.AppliedAt).TotalHours > 24)
            {
                TempData["Message"] =
                    "The 24-hour edit window for this application has passed.";

                return RedirectToAction(nameof(MyApplications));
            }

            var resume = await _context.Resumes
                .FirstOrDefaultAsync(r =>
                    r.Id == resumeId &&
                    r.JobSeekerId == jobSeeker.Id &&
                    r.FilePath != "");

            if (resume == null)
                return NotFound();

            application.ResumeId = resumeId;
            await _context.SaveChangesAsync();

            await RecalculateMatchAsync(application);

            return RedirectToAction(nameof(MyApplications));
        }

        // MY SKILLS

        [HttpGet]
        public async Task<IActionResult> Skills()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            var skills = await _context.JobSeekerSkills
                .Where(s => s.JobSeekerId == jobSeeker.Id)
                .Include(s => s.Skill)
                .OrderBy(s => s.Skill!.Name)
                .ToListAsync();

            return View(skills);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSkill(string skillName)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            if (string.IsNullOrWhiteSpace(skillName))
            {
                TempData["SkillError"] = "Please enter a skill.";
                return RedirectToAction(nameof(Skills));
            }

            var skillNames = skillName
                .Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            int addedCount = 0;

            foreach (var name in skillNames)
            {
                var skill = await _context.Skills
                    .FirstOrDefaultAsync(s =>
                        s.Name.ToLower() == name.ToLower());

                if (skill == null)
                {
                    skill = new Skill { Name = name };

                    _context.Skills.Add(skill);
                    await _context.SaveChangesAsync();
                }

                var alreadyExists = await _context.JobSeekerSkills
                    .AnyAsync(s =>
                        s.JobSeekerId == jobSeeker.Id &&
                        s.SkillId == skill.Id);

                if (!alreadyExists)
                {
                    _context.JobSeekerSkills.Add(new JobSeekerSkill
                    {
                        JobSeekerId = jobSeeker.Id,
                        SkillId = skill.Id
                    });

                    addedCount++;
                }
            }

            await _context.SaveChangesAsync();

            TempData["SkillSuccess"] = addedCount > 0
                ? $"{addedCount} skill(s) added."
                : "You already have these skills.";

            return RedirectToAction(nameof(Skills));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSkill(int id)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            var skill = await _context.JobSeekerSkills
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    s.JobSeekerId == jobSeeker.Id);

            if (skill == null)
                return NotFound();

            _context.JobSeekerSkills.Remove(skill);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Skills));
        }

        // NOTIFICATIONS

        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            var notifications = await _context.Notifications
                .Include(n => n.Job)
                .Include(n => n.JobApplication)
                .Where(n =>
                    n.ApplicationUserId == jobSeeker.ApplicationUserId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();

            return View(notifications);
        }

        // SAVE / UNSAVE JOB

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSaveJob(int jobId)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            var existing = await _context.SavedJobs
                .FirstOrDefaultAsync(s =>
                    s.JobId == jobId &&
                    s.JobSeekerId == jobSeeker.Id);

            if (existing != null)
            {
                _context.SavedJobs.Remove(existing);
            }
            else
            {
                _context.SavedJobs.Add(new SavedJob
                {
                    JobId = jobId,
                    JobSeekerId = jobSeeker.Id
                });
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(Details),
                new { id = jobId });
        }

        // SAVED JOBS

        [HttpGet]
        public async Task<IActionResult> SavedJobs()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            var saved = await _context.SavedJobs
                .Include(s => s.Job)
                    .ThenInclude(j => j!.Employer)
                        .ThenInclude(e => e!.Company)
                .Where(s => s.JobSeekerId == jobSeeker.Id)
                .OrderByDescending(s => s.SavedAt)
                .ToListAsync();

            return View(saved);
        }
    }
}