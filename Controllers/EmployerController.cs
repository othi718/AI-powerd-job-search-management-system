using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AI_powerd_job_search_management_system.Data;
using AI_powerd_job_search_management_system.Models;
using AI_powerd_job_search_management_system.ViewModels;

namespace AI_powerd_job_search_management_system.Controllers
{
    [Authorize(Roles = "Employer")]
    public class EmployerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployerController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        // =========================================================
        // EMPLOYER DASHBOARD
        // =========================================================
        public async Task<IActionResult> Index()
        {
            var employer = await GetCurrentEmployerAsync();

            if (employer == null)
                return RedirectToAction("CompleteProfile");

            var jobs = await _context.Jobs
                .Include(j => j.Applications)
                .Where(j => j.EmployerId == employer.Id)
                .OrderByDescending(j => j.PostedAt)
                .ToListAsync();

            return View(jobs);
        }


        // =========================================================
        // CREATE JOB - GET
        // =========================================================
        [HttpGet]
        public IActionResult CreateJob()
        {
            return View(new JobViewModel());
        }


        // =========================================================
        // CREATE JOB - POST
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateJob(JobViewModel model)
        {
            // -----------------------------------------------------
            // Validate form
            // -----------------------------------------------------
            if (!ModelState.IsValid)
                return View(model);


            // -----------------------------------------------------
            // Get logged-in employer
            // -----------------------------------------------------
            var employer = await GetCurrentEmployerAsync();

            if (employer == null)
                return RedirectToAction("CompleteProfile");


            // =====================================================
            // 1. CREATE JOB
            // =====================================================

            var job = new Job
            {
                EmployerId = employer.Id,
                Title = model.Title,
                Description = model.Description,
                Location = model.Location,
                SalaryRange = model.SalaryRange,
                Status = JobStatus.Open
            };

            _context.Jobs.Add(job);

            // Save first so Job.Id is generated
            await _context.SaveChangesAsync();


            // =====================================================
            // 2. GET REQUIRED SKILLS
            // =====================================================

            var requiredSkillNames = new List<string>();

            if (!string.IsNullOrWhiteSpace(model.RequiredSkills))
            {
                requiredSkillNames = model.RequiredSkills
                    .Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }


            // =====================================================
            // 3. CREATE JOB SKILLS
            // =====================================================

            foreach (var skillName in requiredSkillNames)
            {
                // Check if skill already exists
                var skill = await _context.Skills
                    .FirstOrDefaultAsync(s =>
                        s.Name.ToLower() ==
                        skillName.ToLower());


                // -------------------------------------------------
                // If skill doesn't exist, create it
                // -------------------------------------------------
                if (skill == null)
                {
                    skill = new Skill
                    {
                        Name = skillName
                    };

                    _context.Skills.Add(skill);

                    // Save so Skill.Id is generated
                    await _context.SaveChangesAsync();
                }


                // -------------------------------------------------
                // Check duplicate JobSkill
                // -------------------------------------------------
                var alreadyExists =
                    await _context.JobSkills.AnyAsync(js =>
                        js.JobId == job.Id &&
                        js.SkillId == skill.Id);


                if (!alreadyExists)
                {
                    var jobSkill = new JobSkill
                    {
                        JobId = job.Id,
                        SkillId = skill.Id,
                        IsRequired = true
                    };

                    _context.JobSkills.Add(jobSkill);
                }
            }

            await _context.SaveChangesAsync();


            // =====================================================
            // 4. LOAD JOB REQUIRED SKILLS
            // =====================================================

            var jobSkills = await _context.JobSkills
                .Where(js =>
                    js.JobId == job.Id &&
                    js.IsRequired)
                .Include(js => js.Skill)
                .ToListAsync();


            var requiredSkills = jobSkills
                .Where(js => js.Skill != null)
                .Select(js => js.Skill!.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();


            // =====================================================
            // 5. FIND ALL JOB SEEKERS
            // =====================================================

            var jobSeekers = await _context.JobSeekers
                .Include(js => js.Skills)
                    .ThenInclude(jss => jss.Skill)
                .ToListAsync();


            int notificationCount = 0;


            // =====================================================
            // 6. COMPARE JOB SKILLS WITH JOB SEEKER SKILLS
            // =====================================================

            foreach (var seeker in jobSeekers)
            {
                var candidateSkills = seeker.Skills
                    .Where(s => s.Skill != null)
                    .Select(s => s.Skill!.Name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();


                // -------------------------------------------------
                // Find matching skills
                // -------------------------------------------------

                var matchingSkills = requiredSkills
                    .Where(required =>
                        candidateSkills.Any(candidate =>
                            string.Equals(
                                candidate,
                                required,
                                StringComparison.OrdinalIgnoreCase)))
                    .ToList();


                // -------------------------------------------------
                // Find missing skills
                // -------------------------------------------------

                var missingSkills = requiredSkills
                    .Where(required =>
                        !candidateSkills.Any(candidate =>
                            string.Equals(
                                candidate,
                                required,
                                StringComparison.OrdinalIgnoreCase)))
                    .ToList();


                // =================================================
                // 7. CALCULATE MATCH PERCENTAGE
                // =================================================

                int matchPercentage = 0;

                if (requiredSkills.Count > 0)
                {
                    matchPercentage =
                        (int)Math.Round(
                            (double)matchingSkills.Count /
                            requiredSkills.Count *
                            100);
                }


                // =================================================
                // 8. CREATE NOTIFICATION
                // =================================================

                // Only notify if there is at least one matching
                // skill.
                if (matchingSkills.Any())
                {
                    var matchingText =
                        string.Join(", ", matchingSkills);


                    var missingText =
                        missingSkills.Any()
                            ? string.Join(", ", missingSkills)
                            : "None";


                    var message =
                        $"New job matching your skills: {job.Title}. " +
                        $"Match: {matchPercentage}%. " +
                        $"You have: {matchingText}. " +
                        $"Missing: {missingText}.";


                    var notification = new Notification
                    {
                        ApplicationUserId =
                            seeker.ApplicationUserId,

                        Message = message,

                        IsRead = false,

                        CreatedAt = DateTime.UtcNow
                    };


                    _context.Notifications.Add(notification);

                    notificationCount++;
                }
            }


            // =====================================================
            // 9. SAVE NOTIFICATIONS
            // =====================================================

            await _context.SaveChangesAsync();


            // =====================================================
            // 10. SHOW RESULT TO EMPLOYER
            // =====================================================

            if (requiredSkills.Any())
            {
                TempData["Message"] =
                    $"Job posted successfully. " +
                    $"{notificationCount} Job Seeker(s) " +
                    $"were notified based on matching skills.";
            }
            else
            {
                TempData["Message"] =
                    "Job posted successfully. " +
                    "No required skills were entered.";
            }


            return RedirectToAction("Index");
        }


        // =========================================================
        // EDIT JOB - GET
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> EditJob(int id)
        {
            var employer = await GetCurrentEmployerAsync();

            if (employer == null)
                return RedirectToAction("CompleteProfile");

            var job = await _context.Jobs
                .FirstOrDefaultAsync(j =>
                    j.Id == id &&
                    j.EmployerId == employer.Id);

            if (job == null)
                return NotFound();


            var model = new JobViewModel
            {
                Id = job.Id,
                Title = job.Title,
                Description = job.Description,
                Location = job.Location,
                SalaryRange = job.SalaryRange
            };
            model.RequiredSkills = string.Join(", ", await _context.JobSkills
    .Where(js => js.JobId == job.Id)
    .Include(js => js.Skill)
    .Select(js => js.Skill!.Name)
    .ToListAsync());


            return View(model);
        }


        // =========================================================
        // EDIT JOB - POST
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditJob(
            JobViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);


            var employer = await GetCurrentEmployerAsync();

            if (employer == null)
                return RedirectToAction("CompleteProfile");


            var job = await _context.Jobs
                .FirstOrDefaultAsync(j =>
                    j.Id == model.Id &&
                    j.EmployerId == employer.Id);


            if (job == null)
                return NotFound();


            job.Title = model.Title;
            job.Description = model.Description;
            job.Location = model.Location;
            job.SalaryRange = model.SalaryRange;


            await _context.SaveChangesAsync();
            _context.JobSkills.RemoveRange(_context.JobSkills.Where(js => js.JobId == job.Id));

            if (!string.IsNullOrWhiteSpace(model.RequiredSkills))
            {
                var skillNames = model.RequiredSkills
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var name in skillNames)
                {
                    var skill = await _context.Skills.FirstOrDefaultAsync(s => s.Name.ToLower() == name.ToLower());
                    if (skill == null)
                    {
                        skill = new Skill { Name = name };
                        _context.Skills.Add(skill);
                        await _context.SaveChangesAsync();
                    }
                    _context.JobSkills.Add(new JobSkill { JobId = job.Id, SkillId = skill.Id, IsRequired = true });
                }
            }

            await _context.SaveChangesAsync();


            return RedirectToAction("Index");
        }


        // =========================================================
        // TOGGLE JOB STATUS
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var employer = await GetCurrentEmployerAsync();

            if (employer == null)
                return RedirectToAction("CompleteProfile");


            var job = await _context.Jobs
                .FirstOrDefaultAsync(j =>
                    j.Id == id &&
                    j.EmployerId == employer.Id);


            if (job == null)
                return NotFound();


            if (job.Status == JobStatus.Open)
            {
                job.Status = JobStatus.Closed;
            }
            else
            {
                job.Status = JobStatus.Open;
            }


            await _context.SaveChangesAsync();


            return RedirectToAction("Index");
        }


        // =========================================================
        // COMPLETE EMPLOYER PROFILE - GET
        // =========================================================
        [HttpGet]
        public IActionResult CompleteProfile()
        {
            return View(
                new CompleteEmployerProfileViewModel());
        }


        // =========================================================
        // COMPLETE EMPLOYER PROFILE - POST
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteProfile(
            CompleteEmployerProfileViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);


            var userId = _userManager.GetUserId(User);


            // Safety check
            var existing =
                await _context.Employers
                    .FirstOrDefaultAsync(
                        e => e.ApplicationUserId == userId);


            if (existing != null)
                return RedirectToAction("Index");


            // =====================================================
            // CREATE COMPANY
            // =====================================================

            var company = new Company
            {
                Name = model.CompanyName,
                Website = model.Website,

                // Admin must approve company
                IsApproved = false
            };


            _context.Companies.Add(company);

            await _context.SaveChangesAsync();


            // =====================================================
            // CREATE EMPLOYER
            // =====================================================

            var employer = new Employer
            {
                ApplicationUserId = userId!,
                CompanyId = company.Id,
                JobTitle = model.JobTitle
            };


            _context.Employers.Add(employer);

            await _context.SaveChangesAsync();


            return RedirectToAction("Index");
        }


        // =========================================================
        // GET CURRENT EMPLOYER
        // =========================================================
        private async Task<Employer?> GetCurrentEmployerAsync()
        {
            var userId = _userManager.GetUserId(User);


            return await _context.Employers
                .FirstOrDefaultAsync(
                    e => e.ApplicationUserId == userId);
        }


        // =========================================================
        // APPLICANTS
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Applicants(int jobId)
        {
            var employer = await GetCurrentEmployerAsync();

            if (employer == null)
                return RedirectToAction("CompleteProfile");


            var job = await _context.Jobs
                .FirstOrDefaultAsync(j =>
                    j.Id == jobId &&
                    j.EmployerId == employer.Id);


            if (job == null)
                return NotFound();


            var applications = await _context.JobApplications
       .Include(a => a.JobSeeker)
           .ThenInclude(js => js!.ApplicationUser)
       .Include(a => a.Resume)
       .Include(a => a.AIAnalysis)
       .Where(a => a.JobId == jobId)

                       .OrderByDescending(
                        a => a.AppliedAt)

                    .ToListAsync();


            ViewBag.Job = job;


            return View(applications);
        }


        // =========================================================
        // UPDATE APPLICATION STATUS
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            UpdateApplicationStatus(
                int applicationId,
                ApplicationStatus status)
        {
            var employer =
                await GetCurrentEmployerAsync();


            if (employer == null)
                return RedirectToAction("CompleteProfile");


            var application =
                await _context.JobApplications

                    .Include(a => a.Job)

                    .FirstOrDefaultAsync(a =>
                        a.Id == applicationId &&
                        a.Job!.EmployerId ==
                        employer.Id);


            if (application == null)
                return NotFound();


            application.Status = status;


            await _context.SaveChangesAsync();


            return RedirectToAction(
                "Applicants",
                new
                {
                    jobId = application.JobId
                });
        }
    }
}