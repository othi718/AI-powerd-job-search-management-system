using AI_powerd_job_search_management_system.Data;
using AI_powerd_job_search_management_system.Models;
using AI_powerd_job_search_management_system.Utilities;
using AI_powerd_job_search_management_system.ViewModels;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AI_powerd_job_search_management_system.Services;

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

        // GET CURRENT JOB SEEKER

        private async Task<JobSeeker?> GetCurrentJobSeekerAsync()
        {
            var userId = _userManager.GetUserId(User);

            return await _context.JobSeekers
                .FirstOrDefaultAsync(js => js.ApplicationUserId == userId);
        }


       
        // BROWSE JOBS
  
        public async Task<IActionResult> Index(string? search, string? location, string? category)
        {
            var jobsQuery = _context.Jobs
                .Include(j => j.Employer)
                    .ThenInclude(e => e!.Company)
                .Where(j => j.Status == JobStatus.Open && j.Employer!.Company!.IsApproved);

            if (!string.IsNullOrWhiteSpace(search))
                jobsQuery = jobsQuery.Where(j => j.Title.Contains(search));

            if (!string.IsNullOrWhiteSpace(location))
                jobsQuery = jobsQuery.Where(j => j.Location != null && j.Location.Contains(location));

            if (!string.IsNullOrWhiteSpace(category))
                jobsQuery = jobsQuery.Where(j => j.Category == category);

            var jobs = await jobsQuery.OrderByDescending(j => j.PostedAt).ToListAsync();

            var rawCounts = await _context.Jobs
                .Where(j => j.Status == JobStatus.Open && j.Employer!.Company!.IsApproved)
                .GroupBy(j => j.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Category, x => x.Count);

            var categoryCounts = JobCategories.All.ToDictionary(c => c, c => rawCounts.ContainsKey(c) ? rawCounts[c] : 0);

            ViewBag.Search = search;
            ViewBag.Location = location;
            ViewBag.Category = category;
            ViewBag.CategoryCounts = categoryCounts;
            if (!string.IsNullOrWhiteSpace(category))
            {
                var categoryJobIds = await _context.Jobs
                    .Where(j => j.Status == JobStatus.Open && j.Employer!.Company!.IsApproved && j.Category == category)
                    .Select(j => j.Id)
                    .ToListAsync();

                int totalJobsInCategory = categoryJobIds.Count;

                var skillDemand = await _context.JobSkills
                    .Where(js => categoryJobIds.Contains(js.JobId))
                    .Include(js => js.Skill)
                    .GroupBy(js => js.Skill!.Name)
                    .Select(g => new { Skill = g.Key, JobCount = g.Count() })
                    .OrderByDescending(g => g.JobCount)
                    .Take(6)
                    .ToListAsync();

                ViewBag.SkillLabels = skillDemand.Select(s => s.Skill).ToList();
                ViewBag.SkillPercents = skillDemand
                    .Select(s => totalJobsInCategory > 0 ? Math.Round((double)s.JobCount / totalJobsInCategory * 100, 1) : 0)
                    .ToList();
                ViewBag.TotalJobsInCategory = totalJobsInCategory;
            }

            return View(jobs);
        }

       
        // JOB DETAILS
      
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
                await _context.JobApplications.AnyAsync(
                    a => a.JobId == id &&
                         a.JobSeekerId == jobSeeker.Id);
            ViewBag.IsSaved = jobSeeker != null &&
    await _context.SavedJobs.AnyAsync(s => s.JobId == id && s.JobSeekerId == jobSeeker.Id);
            return View(job);
        }


        // COMPLETE PROFILE - GET
        [HttpGet]
        public async Task<IActionResult> CompleteProfile()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker != null)
                return RedirectToAction(nameof(EditProfile));

            return View(new CompleteJobSeekerProfileViewModel());
        }


        // COMPLETE PROFILE - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteProfile(
            CompleteJobSeekerProfileViewModel model,
            string? next)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            if (!ModelState.IsValid)
                return View(model);

            var jobSeeker = await _context.JobSeekers
                .FirstOrDefaultAsync(
                    js => js.ApplicationUserId == userId);

            if (jobSeeker == null)
            {
                jobSeeker = new JobSeeker
                {
                    ApplicationUserId = userId,
                    Bio = model.Bio,
                    Location = model.Location
                };

                _context.JobSeekers.Add(jobSeeker);
            }
            else
            {
                jobSeeker.Bio = model.Bio;
                jobSeeker.Location = model.Location;
            }

            await _context.SaveChangesAsync();

            if (next == "resumeDetails")
            {
                return await OpenProfileResumeDetailsAsync(jobSeeker.Id);
            }

            return RedirectToAction(nameof(Profile));
        }



        // MY RESUMES

        [HttpGet]
        public async Task<IActionResult> MyResumes()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction("CompleteProfile");

            var resumes = await _context.Resumes
                .Where(r => r.JobSeekerId == jobSeeker.Id)
                .OrderByDescending(r => r.UploadedAt)
                .ToListAsync();

            return View(resumes);
        }



        // PROFILE

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = _userManager.GetUserId(User);

            var jobSeeker = await _context.JobSeekers
                .Include(js => js.Skills).ThenInclude(s => s.Skill)
                .FirstOrDefaultAsync(js => js.ApplicationUserId == userId);

            if (jobSeeker == null) return RedirectToAction("CompleteProfile");

            var user = await _userManager.GetUserAsync(User);
            ViewBag.FullName = user!.FullName;
            ViewBag.Email = user.Email;

            var activeResume = await _context.Resumes
                .Include(r => r.Educations)
                .Include(r => r.Experiences)
                .Include(r => r.Certifications)
                .Include(r => r.Projects)
                .Include(r => r.ExtracurricularActivities)
                .Where(r => r.JobSeekerId == jobSeeker.Id)
                .OrderByDescending(r => r.IsActive)
                .ThenByDescending(r => r.UploadedAt)
                .FirstOrDefaultAsync();

            ViewBag.ActiveResume = activeResume;

            return View(jobSeeker);
        }



        // EDIT PROFILE - GET
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction(nameof(CompleteProfile));

            var model = new CompleteJobSeekerProfileViewModel
            {
                Bio = jobSeeker.Bio,
                Location = jobSeeker.Location
            };

            return View(model);
        }


        // EDIT PROFILE - POST
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



        // UPLOAD RESUME - GET

        [HttpGet]
        public IActionResult UploadResume()
        {
            return View();
        }


        
        // UPLOAD RESUME - POST
 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadResume(
            ResumeUploadViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var allowedExtensions = new[] { ".pdf", ".docx" };

            var extension =
                Path.GetExtension(model.File.FileName)
                    .ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    "",
                    "Only PDF or DOCX files are allowed.");

                return View(model);
            }

            if (model.File.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(
                    "",
                    "File size must be under 5MB.");

                return View(model);
            }

            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction("CompleteProfile");

            var uniqueFileName =
                $"{Guid.NewGuid()}{extension}";

            var uploadPath = Path.Combine(
                _env.ContentRootPath,
                "UploadedFiles",
                "Resumes");

            Directory.CreateDirectory(uploadPath);

            var fullPath =
                Path.Combine(uploadPath, uniqueFileName);

            using (var stream =
                   new FileStream(fullPath, FileMode.Create))
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

            return RedirectToAction(
                nameof(ResumeDetails),
                new { resumeId = resume.Id });
        }


        // DELETE RESUME
    
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteResume(int id)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction("CompleteProfile");

            var resume = await _context.Resumes
                .FirstOrDefaultAsync(r =>
                    r.Id == id &&
                    r.JobSeekerId == jobSeeker.Id);

            if (resume == null)
                return NotFound();

            var fullPath = Path.Combine(
                _env.ContentRootPath,
                "UploadedFiles",
                "Resumes",
                resume.FilePath);

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }

            _context.Resumes.Remove(resume);

            await _context.SaveChangesAsync();

            return RedirectToAction("MyResumes");
        }
        private async Task RecalculateMatchAsync(JobApplication application)
        {
            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == application.JobId);

            var requiredSkills = await _context.JobSkills
                .Where(js => js.JobId == application.JobId)
                .Include(js => js.Skill)
                .Select(js => js.Skill!.Name.Trim())
                .ToListAsync();

            var candidateSkills = await _context.JobSeekerSkills
                .Where(s => s.JobSeekerId == application.JobSeekerId)
                .Include(s => s.Skill)
                .Select(s => s.Skill!.Name.Trim())
                .ToListAsync();

            var matchedSkills = requiredSkills.Where(r => candidateSkills.Any(c => SkillMatcher.IsMatch(c, r))).ToList();
            var missingSkills = requiredSkills.Where(r => !candidateSkills.Any(c => SkillMatcher.IsMatch(c, r))).ToList();

            double score = requiredSkills.Any()
                ? Math.Round((double)matchedSkills.Count / requiredSkills.Count * 100, 1)
                : 0;

            application.MatchScore = score;

            var analysis = await _context.AIAnalyses.FirstOrDefaultAsync(a => a.JobApplicationId == application.Id);
            if (analysis == null)
            {
                analysis = new AIAnalysis { JobApplicationId = application.Id };
                _context.AIAnalyses.Add(analysis);
            }
            analysis.MatchedSkills = matchedSkills.Any() ? string.Join(", ", matchedSkills) : "None";
            analysis.MissingSkills = missingSkills.Any() ? string.Join(", ", missingSkills) : "None";
            analysis.SkillMatchScore = score;
            analysis.OverallScore = score;

            if (job != null)
            {
                analysis.AIInsight = await _gemini.GenerateMatchInsightAsync(
                    job.Title, job.Description, requiredSkills, candidateSkills, score);
            }

            await _context.SaveChangesAsync();
        }
        private async Task<Resume?> GetOwnedResumeAsync(int resumeId)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();
            if (jobSeeker == null) return null;
            return await _context.Resumes.FirstOrDefaultAsync(r => r.Id == resumeId && r.JobSeekerId == jobSeeker.Id);
        }

        [HttpGet]
        public async Task<IActionResult> ResumeDetails(int resumeId)
        {
            var resume = await GetOwnedResumeAsync(resumeId);
            if (resume == null) return NotFound();

            ViewBag.Educations = await _context.Educations.Where(e => e.ResumeId == resumeId).ToListAsync();
            ViewBag.Experiences = await _context.Experiences.Where(e => e.ResumeId == resumeId).ToListAsync();
            ViewBag.Certifications = await _context.Certifications.Where(c => c.ResumeId == resumeId).ToListAsync();
            ViewBag.Projects = await _context.Projects.Where(p => p.ResumeId == resumeId).ToListAsync();
            ViewBag.Activities = await _context.ExtracurricularActivities.Where(a => a.ResumeId == resumeId).ToListAsync();
            ViewBag.Resume = resume;

            return View();
        }

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

            if (!ModelState.IsValid)
            {
                TempData["ResumeError"] =
                    "Education could not be saved. Check the fields and dates.";

                return RedirectToAction("ResumeDetails", new { resumeId });
            }

            if (string.IsNullOrWhiteSpace(institution))
            {
                TempData["ResumeError"] = "Please enter the institution name.";

                return RedirectToAction("ResumeDetails", new { resumeId });
            }

            institution = institution.Trim();
            degree = degree?.Trim();
            fieldOfStudy = fieldOfStudy?.Trim();

            if (institution.Length > 200 ||
                (degree?.Length ?? 0) > 150 ||
                (fieldOfStudy?.Length ?? 0) > 150)
            {
                TempData["ResumeError"] =
                    "Institution must be within 200 characters. " +
                    "Degree and field of study must be within 150 characters.";

                return RedirectToAction("ResumeDetails", new { resumeId });
            }

            if (startDate.HasValue &&
                endDate.HasValue &&
                endDate.Value.Date < startDate.Value.Date)
            {
                TempData["ResumeError"] =
                    "Education end date cannot be earlier than the start date.";

                return RedirectToAction("ResumeDetails", new { resumeId });
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

            return RedirectToAction("ResumeDetails", new { resumeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEducation(int id, int resumeId)
        {
            var resume = await GetOwnedResumeAsync(resumeId);
            if (resume == null) return NotFound();
            var item = await _context.Educations.FirstOrDefaultAsync(e => e.Id == id && e.ResumeId == resumeId);
            if (item != null) { _context.Educations.Remove(item); await _context.SaveChangesAsync(); }
            return RedirectToAction("ResumeDetails", new { resumeId });
        }

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

            if (!ModelState.IsValid)
            {
                TempData["ResumeError"] =
                    "Experience could not be saved. Check the fields and dates.";

                return RedirectToAction("ResumeDetails", new { resumeId });
            }

            if (string.IsNullOrWhiteSpace(companyName) ||
                string.IsNullOrWhiteSpace(jobTitle))
            {
                TempData["ResumeError"] =
                    "Please enter both the company name and job title.";

                return RedirectToAction("ResumeDetails", new { resumeId });
            }

            companyName = companyName.Trim();
            jobTitle = jobTitle.Trim();

            if (companyName.Length > 150 || jobTitle.Length > 150)
            {
                TempData["ResumeError"] =
                    "Company name and job title must each be within 150 characters.";

                return RedirectToAction("ResumeDetails", new { resumeId });
            }

            if (startDate.HasValue &&
                endDate.HasValue &&
                endDate.Value.Date < startDate.Value.Date)
            {
                TempData["ResumeError"] =
                    "Experience end date cannot be earlier than the start date.";

                return RedirectToAction("ResumeDetails", new { resumeId });
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

            return RedirectToAction("ResumeDetails", new { resumeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteExperience(int id, int resumeId)
        {
            var resume = await GetOwnedResumeAsync(resumeId);
            if (resume == null) return NotFound();
            var item = await _context.Experiences.FirstOrDefaultAsync(e => e.Id == id && e.ResumeId == resumeId);
            if (item != null) { _context.Experiences.Remove(item); await _context.SaveChangesAsync(); }
            return RedirectToAction("ResumeDetails", new { resumeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCertification(int resumeId, string name, string? issuingOrganization, DateTime? dateIssued)
        {
            var resume = await GetOwnedResumeAsync(resumeId);
            if (resume == null) return NotFound();
            if (!string.IsNullOrWhiteSpace(name))
            {
                _context.Certifications.Add(new Certification { ResumeId = resumeId, Name = name, IssuingOrganization = issuingOrganization, DateIssued = dateIssued });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("ResumeDetails", new { resumeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCertification(int id, int resumeId)
        {
            var resume = await GetOwnedResumeAsync(resumeId);
            if (resume == null) return NotFound();
            var item = await _context.Certifications.FirstOrDefaultAsync(c => c.Id == id && c.ResumeId == resumeId);
            if (item != null) { _context.Certifications.Remove(item); await _context.SaveChangesAsync(); }
            return RedirectToAction("ResumeDetails", new { resumeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddProject(int resumeId, string title, string? description, string? technologiesUsed)
        {
            var resume = await GetOwnedResumeAsync(resumeId);
            if (resume == null) return NotFound();
            if (!string.IsNullOrWhiteSpace(title))
            {
                _context.Projects.Add(new Project { ResumeId = resumeId, Title = title, Description = description, TechnologiesUsed = technologiesUsed });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("ResumeDetails", new { resumeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProject(int id, int resumeId)
        {
            var resume = await GetOwnedResumeAsync(resumeId);
            if (resume == null) return NotFound();
            var item = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id && p.ResumeId == resumeId);
            if (item != null) { _context.Projects.Remove(item); await _context.SaveChangesAsync(); }
            return RedirectToAction("ResumeDetails", new { resumeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddActivity(int resumeId, string activityName, string? roleOrPosition, string? description)
        {
            var resume = await GetOwnedResumeAsync(resumeId);
            if (resume == null) return NotFound();
            if (!string.IsNullOrWhiteSpace(activityName))
            {
                _context.ExtracurricularActivities.Add(new ExtracurricularActivity { ResumeId = resumeId, ActivityName = activityName, RoleOrPosition = roleOrPosition, Description = description });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("ResumeDetails", new { resumeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteActivity(int id, int resumeId)
        {
            var resume = await GetOwnedResumeAsync(resumeId);
            if (resume == null) return NotFound();
            var item = await _context.ExtracurricularActivities.FirstOrDefaultAsync(a => a.Id == id && a.ResumeId == resumeId);
            if (item != null) { _context.ExtracurricularActivities.Remove(item); await _context.SaveChangesAsync(); }
            return RedirectToAction("ResumeDetails", new { resumeId });
        }


        // APPLY - GET

        [HttpGet]
        public async Task<IActionResult> Apply(int jobId)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction("CompleteProfile");

            var job = await _context.Jobs
                .Include(j => j.Employer)
                    .ThenInclude(e => e!.Company)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null)
                return NotFound();

            var alreadyApplied =
                await _context.JobApplications.AnyAsync(
                    a => a.JobId == jobId &&
                         a.JobSeekerId == jobSeeker.Id);

            if (alreadyApplied)
                return RedirectToAction("MyApplications");

            var resumes = await _context.Resumes
                .Where(r => r.JobSeekerId == jobSeeker.Id)
                .OrderByDescending(r => r.UploadedAt)
                .ToListAsync();

            if (!resumes.Any())
            {
                TempData["Message"] =
                    "Upload a resume before applying.";

                return RedirectToAction("UploadResume");
            }

            ViewBag.Job = job;
            ViewBag.Resumes = resumes;

            return View();
        }


       
        // APPLY - POST
       
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

            //  AI matching: compare job's required skills vs this job seeker's skills 
            await RecalculateMatchAsync(application);

         
            await _context.SaveChangesAsync();

            return RedirectToAction("MyApplications");
        }


       
        // MY APPLICATIONS
      
        [HttpGet]
        public async Task<IActionResult> MyApplications()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction("CompleteProfile");

            var applications = await _context.JobApplications
                .Include(a => a.Job)
                    .ThenInclude(j => j!.Employer)
                        .ThenInclude(e => e!.Company)
                .Include(a => a.AIAnalysis)
                .Where(a =>
                    a.JobSeekerId == jobSeeker.Id)
                .OrderByDescending(a => a.AppliedAt)
                .ToListAsync();

            return View(applications);
        }
        [HttpGet]
        public async Task<IActionResult> EditApplication(int id)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();
            if (jobSeeker == null) return RedirectToAction("CompleteProfile");

            var application = await _context.JobApplications
                .FirstOrDefaultAsync(a => a.Id == id && a.JobSeekerId == jobSeeker.Id);
            if (application == null) return NotFound();

            if ((DateTime.UtcNow - application.AppliedAt).TotalHours > 24)
            {
                TempData["Message"] = "The 24-hour edit window for this application has passed.";
                return RedirectToAction("MyApplications");
            }

            var resumes = await _context.Resumes
                .Where(r => r.JobSeekerId == jobSeeker.Id)
                .OrderByDescending(r => r.UploadedAt)
                .ToListAsync();

            ViewBag.Application = application;
            ViewBag.Resumes = resumes;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditApplication(int id, int resumeId)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();
            if (jobSeeker == null) return RedirectToAction("CompleteProfile");

            var application = await _context.JobApplications
                .FirstOrDefaultAsync(a => a.Id == id && a.JobSeekerId == jobSeeker.Id);
            if (application == null) return NotFound();

            if ((DateTime.UtcNow - application.AppliedAt).TotalHours > 24)
            {
                TempData["Message"] = "The 24-hour edit window for this application has passed.";
                return RedirectToAction("MyApplications");
            }

            var resume = await _context.Resumes
                .FirstOrDefaultAsync(r => r.Id == resumeId && r.JobSeekerId == jobSeeker.Id);
            if (resume == null) return NotFound();

            application.ResumeId = resumeId;
            await _context.SaveChangesAsync();

            await RecalculateMatchAsync(application);

            return RedirectToAction("MyApplications");
        }

        // MY SKILLS

        [HttpGet]
        public async Task<IActionResult> Skills()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction("CompleteProfile");

            var skills = await _context.JobSeekerSkills
                .Where(s => s.JobSeekerId == jobSeeker.Id)
                .Include(s => s.Skill)
                .OrderBy(s => s.Skill!.Name)
                .ToListAsync();

            return View(skills);
        }


      
        // ADD SKILL
     
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSkill(string skillName)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();
            if (jobSeeker == null) return RedirectToAction("CompleteProfile");

            if (string.IsNullOrWhiteSpace(skillName))
            {
                TempData["SkillError"] = "Please enter a skill.";
                return RedirectToAction("Skills");
            }

            var skillNames = skillName
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            int addedCount = 0;

            foreach (var name in skillNames)
            {
                var skill = await _context.Skills.FirstOrDefaultAsync(s => s.Name.ToLower() == name.ToLower());
                if (skill == null)
                {
                    skill = new Skill { Name = name };
                    _context.Skills.Add(skill);
                    await _context.SaveChangesAsync();
                }

                var alreadyExists = await _context.JobSeekerSkills
                    .AnyAsync(s => s.JobSeekerId == jobSeeker.Id && s.SkillId == skill.Id);

                if (!alreadyExists)
                {
                    _context.JobSeekerSkills.Add(new JobSeekerSkill { JobSeekerId = jobSeeker.Id, SkillId = skill.Id });
                    addedCount++;
                }
            }

            await _context.SaveChangesAsync();

            TempData["SkillSuccess"] = addedCount > 0 ? $"{addedCount} skill(s) added." : "You already have these skills.";
            return RedirectToAction("Skills");
        }

     
        // DELETE SKILL
 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSkill(int id)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction("CompleteProfile");

            var skill = await _context.JobSeekerSkills
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    s.JobSeekerId == jobSeeker.Id);

            if (skill == null)
                return NotFound();

            _context.JobSeekerSkills.Remove(skill);

            await _context.SaveChangesAsync();

            return RedirectToAction("Skills");
        }


        // NOTIFICATIONS
       
        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();

            if (jobSeeker == null)
                return RedirectToAction("CompleteProfile");

            var notifications = await _context.Notifications
       .Include(n => n.Job)
       .Include(n => n.JobApplication)
       .Where(n => n.ApplicationUserId == jobSeeker.ApplicationUserId)
       .OrderByDescending(n => n.CreatedAt)
       .ToListAsync();
            // Mark notifications as read
            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();

            return View(notifications);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSaveJob(int jobId)
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();
            if (jobSeeker == null) return RedirectToAction("CompleteProfile");

            var existing = await _context.SavedJobs
                .FirstOrDefaultAsync(s => s.JobId == jobId && s.JobSeekerId == jobSeeker.Id);

            if (existing != null)
                _context.SavedJobs.Remove(existing);
            else
                _context.SavedJobs.Add(new SavedJob { JobId = jobId, JobSeekerId = jobSeeker.Id });

            await _context.SaveChangesAsync();
            return RedirectToAction("Details", new { id = jobId });
        }

        [HttpGet]
        public async Task<IActionResult> SavedJobs()
        {
            var jobSeeker = await GetCurrentJobSeekerAsync();
            if (jobSeeker == null) return RedirectToAction("CompleteProfile");

            var saved = await _context.SavedJobs
                .Include(s => s.Job)
                    .ThenInclude(j => j!.Employer)
                        .ThenInclude(e => e!.Company)
                .Where(s => s.JobSeekerId == jobSeeker.Id)
                .OrderByDescending(s => s.SavedAt)
                .ToListAsync();

            return View(saved);
        }
        private async Task<IActionResult> OpenProfileResumeDetailsAsync(
    int jobSeekerId)
        {
            var resumeId = await _context.Resumes
                .AsNoTracking()
                .Where(r => r.JobSeekerId == jobSeekerId)
                .OrderByDescending(r => r.IsActive)
                .ThenByDescending(r => r.UploadedAt)
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync();

            if (resumeId.HasValue)
            {
                return RedirectToAction(
                    nameof(ResumeDetails),
                    new { resumeId = resumeId.Value });
            }

            return RedirectToAction(nameof(UploadResume));
        }

    }
}