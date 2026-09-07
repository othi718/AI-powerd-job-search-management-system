using AI_powerd_job_search_management_system.Data;
using AI_powerd_job_search_management_system.Models;
using AI_powerd_job_search_management_system.Utilities;
using AI_powerd_job_search_management_system.ViewModels;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        // EMPLOYER DASHBOARD
        public async Task<IActionResult> Index(string? search, string? status)
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null)
                return RedirectToAction("CompleteProfile");

            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Id == employer.CompanyId);

            ViewBag.Company = company;

            var jobsQuery = _context.Jobs
                .Include(j => j.Applications)
                .Where(j => j.EmployerId == employer.Id);

            if (!string.IsNullOrWhiteSpace(search))
                jobsQuery = jobsQuery.Where(j => j.Title.Contains(search));

            if (status == "Open")
                jobsQuery = jobsQuery.Where(j => j.Status == JobStatus.Open);
            else if (status == "Closed")
                jobsQuery = jobsQuery.Where(j => j.Status == JobStatus.Closed);

            var jobs = await jobsQuery
                .OrderByDescending(j => j.PostedAt)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Status = status;

            return View(jobs);
        }

        // CREATE JOB - GET
        [HttpGet]
        public async Task<IActionResult> CreateJob()
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null) return RedirectToAction("CompleteProfile");

            var company = await _context.Companies.FindAsync(employer.CompanyId);
            if (company == null || !company.IsApproved)
            {
                TempData["Message"] = "Your company is still awaiting Admin approval. You'll be able to post jobs once approved.";
                return RedirectToAction("Index");
            }
            if (employer.Position != EmployerPosition.Owner && !employer.IsApprovedByOwner)
            {
                TempData["Message"] = "Your account is awaiting approval from your company's Owner before you can post jobs.";
                return RedirectToAction("Index");
            }

            ViewBag.Branches = await GetAvailableBranchesAsync(employer);

            return View(new JobViewModel { BranchId = employer.BranchId });
        }

        // CREATE JOB - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateJob(JobViewModel model)
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null) return RedirectToAction("CompleteProfile");

            if (!ModelState.IsValid)
            {
                ViewBag.Branches = await GetAvailableBranchesAsync(employer);
                return View(model);
            }

            var company = await _context.Companies.FindAsync(employer.CompanyId);
            if (company == null || !company.IsApproved)
            {
                TempData["Message"] = "Your company is still awaiting Admin approval. You'll be able to post jobs once approved.";
                return RedirectToAction("Index");
            }
            if (employer.Position != EmployerPosition.Owner && !employer.IsApprovedByOwner)
            {
                TempData["Message"] = "Your account is awaiting approval from your company's Owner before you can post jobs.";
                return RedirectToAction("Index");
            }

            bool branchAllowed = await IsBranchAllowedAsync(employer, model.BranchId);
            if (!branchAllowed)
            {
                ModelState.AddModelError(nameof(model.BranchId), "Select a branch you are allowed to post for.");
                ViewBag.Branches = await GetAvailableBranchesAsync(employer);
                return View(model);
            }

            // 1. CREATE JOB
            var job = new Job
            {
                EmployerId = employer.Id,
                BranchId = model.BranchId,
                Title = model.Title,
                Description = model.Description,
                Location = model.Location,
                SalaryRange = model.SalaryRange,
                Category = model.Category,
                Deadline = model.Deadline,
                Status = JobStatus.Open,
                EducationRequirement = model.EducationRequirement,
                ExperienceRequirement = model.ExperienceRequirement,
                AdditionalRequirements = model.AdditionalRequirements,
                Responsibilities = model.Responsibilities,
                Benefits = model.Benefits,
            };

            _context.Jobs.Add(job);
            await _context.SaveChangesAsync();

            // 2. GET REQUIRED SKILLS
            var requiredSkillNames = new List<string>();
            if (!string.IsNullOrWhiteSpace(model.RequiredSkills))
            {
                requiredSkillNames = model.RequiredSkills
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            // 3. CREATE JOB SKILLS
            foreach (var skillName in requiredSkillNames)
            {
                var skill = await _context.Skills.FirstOrDefaultAsync(s => s.Name.ToLower() == skillName.ToLower());
                if (skill == null)
                {
                    skill = new Skill { Name = skillName };
                    _context.Skills.Add(skill);
                    await _context.SaveChangesAsync();
                }

                var alreadyExists = await _context.JobSkills.AnyAsync(js => js.JobId == job.Id && js.SkillId == skill.Id);
                if (!alreadyExists)
                {
                    _context.JobSkills.Add(new JobSkill { JobId = job.Id, SkillId = skill.Id, IsRequired = true });
                }
            }
            await _context.SaveChangesAsync();

            // 4. LOAD JOB REQUIRED SKILLS
            var jobSkills = await _context.JobSkills
                .Where(js => js.JobId == job.Id && js.IsRequired)
                .Include(js => js.Skill)
                .ToListAsync();

            var requiredSkills = jobSkills
                .Where(js => js.Skill != null)
                .Select(js => js.Skill!.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 5. FIND ALL JOB SEEKERS
            var jobSeekers = await _context.JobSeekers
                .Include(js => js.Skills)
                    .ThenInclude(jss => jss.Skill)
                .ToListAsync();

            int notificationCount = 0;

            // 6. COMPARE JOB SKILLS WITH JOB SEEKER SKILLS
            foreach (var seeker in jobSeekers)
            {
                var candidateSkills = seeker.Skills
                    .Where(s => s.Skill != null)
                    .Select(s => s.Skill!.Name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var matchingSkills = requiredSkills
                    .Where(required => candidateSkills.Any(candidate => SkillMatcher.IsMatch(candidate, required)))
                    .ToList();

                var missingSkills = requiredSkills
                    .Where(required => !candidateSkills.Any(candidate => SkillMatcher.IsMatch(candidate, required)))
                    .ToList();

                int matchPercentage = 0;
                if (requiredSkills.Count > 0)
                {
                    matchPercentage = (int)Math.Round((double)matchingSkills.Count / requiredSkills.Count * 100);
                }

                if (matchingSkills.Any())
                {
                    var matchingText = string.Join(", ", matchingSkills);
                    var missingText = missingSkills.Any() ? string.Join(", ", missingSkills) : "None";

                    var message = $"New job matching your skills: {job.Title}. " +
                        $"Match: {matchPercentage}%. You have: {matchingText}. Missing: {missingText}.";

                    _context.Notifications.Add(new Notification
                    {
                        ApplicationUserId = seeker.ApplicationUserId,
                        JobId = job.Id,
                        Message = message,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });

                    notificationCount++;
                }
            }

            await _context.SaveChangesAsync();

            TempData["Message"] = requiredSkills.Any()
                ? $"Job posted successfully. {notificationCount} Job Seeker(s) were notified based on matching skills."
                : "Job posted successfully. No required skills were entered.";

            return RedirectToAction("Index");
        }

        // EDIT JOB - GET
        [HttpGet]
        public async Task<IActionResult> EditJob(int id)
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null) return RedirectToAction("CompleteProfile");

            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.EmployerId == employer.Id);
            if (job == null) return NotFound();

            var model = new JobViewModel
            {
                Id = job.Id,
                Title = job.Title,
                Description = job.Description,
                Location = job.Location,
                SalaryRange = job.SalaryRange,
                Category = job.Category,
                Deadline = job.Deadline,
                BranchId = job.BranchId ?? 0,
                EducationRequirement = job.EducationRequirement,
                ExperienceRequirement = job.ExperienceRequirement,
                AdditionalRequirements = job.AdditionalRequirements,
                Responsibilities = job.Responsibilities,
                Benefits = job.Benefits,
            };
            model.RequiredSkills = string.Join(", ", await _context.JobSkills
                .Where(js => js.JobId == job.Id)
                .Include(js => js.Skill)
                .Select(js => js.Skill!.Name)
                .ToListAsync());

            ViewBag.Branches = await GetAvailableBranchesAsync(employer);

            return View(model);
        }

        // EDIT JOB - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditJob(JobViewModel model)
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null) return RedirectToAction("CompleteProfile");

            if (!ModelState.IsValid)
            {
                ViewBag.Branches = await GetAvailableBranchesAsync(employer);
                return View(model);
            }

            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == model.Id && j.EmployerId == employer.Id);
            if (job == null) return NotFound();

            bool branchAllowed = await IsBranchAllowedAsync(employer, model.BranchId);
            if (!branchAllowed)
            {
                ModelState.AddModelError(nameof(model.BranchId), "Select a branch you are allowed to post for.");
                ViewBag.Branches = await GetAvailableBranchesAsync(employer);
                return View(model);
            }

            job.Title = model.Title;
            job.Description = model.Description;
            job.Location = model.Location;
            job.SalaryRange = model.SalaryRange;
            job.Category = model.Category;
            job.Deadline = model.Deadline;
            job.BranchId = model.BranchId;
            job.EducationRequirement = model.EducationRequirement;
            job.ExperienceRequirement = model.ExperienceRequirement;
            job.AdditionalRequirements = model.AdditionalRequirements;
            job.Responsibilities = model.Responsibilities;
            job.Benefits = model.Benefits;

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

        // TOGGLE JOB STATUS
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null) return RedirectToAction("CompleteProfile");

            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.EmployerId == employer.Id);
            if (job == null) return NotFound();

            job.Status = job.Status == JobStatus.Open ? JobStatus.Closed : JobStatus.Open;
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // COMPLETE EMPLOYER PROFILE - GET (chooser)
        [HttpGet]
        public IActionResult CompleteProfile()
        {
            return View();
        }

        // OWNER PATH
        [HttpGet]
        public IActionResult CompleteProfileOwner()
        {
            return View(new CompleteEmployerProfileViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteProfileOwner(CompleteEmployerProfileViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var userId = _userManager.GetUserId(User);

            var existingEmployer = await _context.Employers.FirstOrDefaultAsync(e => e.ApplicationUserId == userId);
            if (existingEmployer != null)
            {
                TempData["Message"] = "You already have a company profile set up.";
                return RedirectToAction("Index");
            }

            var existingCompany = await _context.Companies
                .FirstOrDefaultAsync(c => c.Name.ToLower() == model.CompanyName.ToLower());
            if (existingCompany != null)
            {
                ModelState.AddModelError("", "A company with this name already exists. Please join as staff instead.");
                return View(model);
            }

            var company = new Company
            {
                Name = model.CompanyName,
                Website = model.Website,
                ContactEmail = model.ContactEmail,
                Phone = model.Phone,
                Location = model.Location,
                IsApproved = false
            };
            _context.Companies.Add(company);
            await _context.SaveChangesAsync();

            var branch = new Branch { CompanyId = company.Id, Name = "Main Branch", Location = model.Location };
            _context.Branches.Add(branch);
            await _context.SaveChangesAsync();

            var employer = new Employer
            {
                ApplicationUserId = userId!,
                CompanyId = company.Id,
                BranchId = branch.Id,
                JobTitle = model.JobTitle,
                Position = EmployerPosition.Owner,
                IsApprovedByOwner = true
            };
            _context.Employers.Add(employer);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Company submitted for Admin approval.";
            return RedirectToAction("Index");
        }

        // STAFF PATH (now with branch selection)
        [HttpGet]
        public async Task<IActionResult> CompleteProfileStaff()
        {
            var companies = await _context.Companies
                .Where(c => c.IsApproved)
                .Include(c => c.Branches)
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    Branches = c.Branches
                        .OrderBy(b => b.Name)
                        .Select(b => new { b.Id, b.Name })
                })
                .ToListAsync();

            ViewBag.CompaniesJson = System.Text.Json.JsonSerializer.Serialize(companies);
            return View(new JoinCompanyViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteProfileStaff(JoinCompanyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var companiesInvalid = await _context.Companies
                    .Where(c => c.IsApproved)
                    .Include(c => c.Branches)
                    .OrderBy(c => c.Name)
                    .Select(c => new
                    {
                        c.Id,
                        c.Name,
                        Branches = c.Branches
                            .OrderBy(b => b.Name)
                            .Select(b => new { b.Id, b.Name })
                    })
                    .ToListAsync();
                ViewBag.CompaniesJson = System.Text.Json.JsonSerializer.Serialize(companiesInvalid);
                return View(model);
            }

            var userId = _userManager.GetUserId(User);

            var existingEmployer = await _context.Employers.FirstOrDefaultAsync(e => e.ApplicationUserId == userId);
            if (existingEmployer != null)
            {
                TempData["Message"] = "You already have a company profile set up.";
                return RedirectToAction("Index");
            }

            var companyExists = await _context.Companies
                .AnyAsync(c => c.Id == model.CompanyId && c.IsApproved);

            var branch = await _context.Branches
                .FirstOrDefaultAsync(b =>
                    b.Id == model.BranchId &&
                    b.CompanyId == model.CompanyId);

            if (!companyExists || branch == null)
            {
                ModelState.AddModelError("", "Select a registered company and one of its branches.");
                var companiesRetry = await _context.Companies
                    .Where(c => c.IsApproved)
                    .Include(c => c.Branches)
                    .OrderBy(c => c.Name)
                    .Select(c => new
                    {
                        c.Id,
                        c.Name,
                        Branches = c.Branches
                            .OrderBy(b => b.Name)
                            .Select(b => new { b.Id, b.Name })
                    })
                    .ToListAsync();
                ViewBag.CompaniesJson = System.Text.Json.JsonSerializer.Serialize(companiesRetry);
                return View(model);
            }

            var employer = new Employer
            {
                ApplicationUserId = userId!,
                CompanyId = model.CompanyId,
                BranchId = branch.Id,
                JobTitle = model.JobTitle,
                Position = model.Position,
                IsApprovedByOwner = false
            };
            _context.Employers.Add(employer);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Your request to join has been sent to the company owner for approval.";
            return RedirectToAction("Index");
        }

        // BRANCHES: owner sees all company branches; staff sees only assigned branch
        [HttpGet]
        public async Task<IActionResult> Branches()
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null)
                return RedirectToAction("CompleteProfile");

            bool isOwner = employer.Position == EmployerPosition.Owner;

            var branchesQuery = _context.Branches
                .Where(b => b.CompanyId == employer.CompanyId);

            if (!isOwner)
                branchesQuery = branchesQuery.Where(b => b.Id == employer.BranchId);

            var branches = await branchesQuery
                .OrderBy(b => b.Name)
                .ToListAsync();

            var branchInfo = new List<(Branch branch, int staffCount, int jobCount)>();
            foreach (var b in branches)
            {
                int staffCount = await _context.Employers.CountAsync(e =>
                    e.CompanyId == employer.CompanyId &&
                    e.BranchId == b.Id &&
                    e.IsApprovedByOwner &&
                    e.Position != EmployerPosition.Owner);

                int jobCount = await _context.Jobs.CountAsync(j => j.BranchId == b.Id);
                branchInfo.Add((b, staffCount, jobCount));
            }

            ViewBag.BranchInfo = branchInfo;
            ViewBag.IsOwner = isOwner;
            return View();
        }

        // BRANCH DETAILS: owner can manage; staff can only view assigned branch
        [HttpGet]
        public async Task<IActionResult> BranchDetails(int id)
        {
            var employer = await GetCurrentEmployerAsync();

            if (employer == null)
                return RedirectToAction("CompleteProfile");

            bool isOwner = employer.Position == EmployerPosition.Owner;

            if (!isOwner && employer.BranchId != id)
                return Forbid();

            var branch = await _context.Branches
                .Include(b => b.Company)
                .FirstOrDefaultAsync(b =>
                    b.Id == id &&
                    b.CompanyId == employer.CompanyId);

            if (branch == null)
                return NotFound();

            var approvedStaff = await _context.Employers
                .Include(e => e.ApplicationUser)
                .Where(e =>
                    e.BranchId == id &&
                    e.CompanyId == employer.CompanyId &&
                    e.Position != EmployerPosition.Owner &&
                    e.IsApprovedByOwner)
                .OrderBy(e => e.ApplicationUser!.FullName)
                .ToListAsync();

            var pendingStaff = isOwner
                ? await _context.Employers
                    .Include(e => e.ApplicationUser)
                    .Where(e =>
                        e.BranchId == id &&
                        e.CompanyId == employer.CompanyId &&
                        e.Position != EmployerPosition.Owner &&
                        !e.IsApprovedByOwner)
                    .OrderBy(e => e.CreatedAt)
                    .ToListAsync()
                : new List<Employer>();

            var jobs = await _context.Jobs
                .Include(j => j.Employer)
                    .ThenInclude(e => e!.ApplicationUser)
                .Where(j =>
                    j.BranchId == id &&
                    j.Employer!.CompanyId == employer.CompanyId)
                .OrderByDescending(j => j.PostedAt)
                .ToListAsync();

            ViewBag.ApprovedStaff = approvedStaff;
            ViewBag.PendingStaff = pendingStaff;
            ViewBag.Jobs = jobs;
            ViewBag.IsOwner = isOwner;

            return View(branch);
        }

        [HttpGet]
        public async Task<IActionResult> CreateBranch()
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null || employer.Position != EmployerPosition.Owner) return Forbid();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBranch(string name, string? location)
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null || employer.Position != EmployerPosition.Owner) return Forbid();

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Message"] = "Branch name is required.";
                return RedirectToAction("CreateBranch");
            }

            _context.Branches.Add(new Branch { CompanyId = employer.CompanyId, Name = name.Trim(), Location = location });
            await _context.SaveChangesAsync();

            TempData["Message"] = "Branch created.";
            return RedirectToAction("Branches");
        }

        private async Task<List<Branch>> GetAvailableBranchesAsync(Employer employer)
        {
            var query = _context.Branches
                .Where(b => b.CompanyId == employer.CompanyId);

            if (employer.Position != EmployerPosition.Owner)
                query = query.Where(b => b.Id == employer.BranchId);

            return await query.OrderBy(b => b.Name).ToListAsync();
        }

        private async Task<bool> IsBranchAllowedAsync(Employer employer, int branchId)
        {
            return await _context.Branches.AnyAsync(b =>
                b.Id == branchId &&
                b.CompanyId == employer.CompanyId &&
                (employer.Position == EmployerPosition.Owner || b.Id == employer.BranchId));
        }

        // GET CURRENT EMPLOYER
        private async Task<Employer?> GetCurrentEmployerAsync()
        {
            var userId = _userManager.GetUserId(User);
            return await _context.Employers.FirstOrDefaultAsync(e => e.ApplicationUserId == userId);
        }

        // APPLICANTS
        [HttpGet]
        public async Task<IActionResult> Applicants(int jobId)
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null) return RedirectToAction("CompleteProfile");

            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId && j.EmployerId == employer.Id);
            if (job == null) return NotFound();

            var applications = await _context.JobApplications
                .Include(a => a.JobSeeker).ThenInclude(js => js!.ApplicationUser)
                .Include(a => a.Resume)
                .Include(a => a.AIAnalysis)
                .Where(a => a.JobId == jobId)
                .OrderByDescending(a => a.AppliedAt)
                .ToListAsync();

            ViewBag.Job = job;
            return View(applications);
        }

        // UPDATE APPLICATION STATUS
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateApplicationStatus(int applicationId, ApplicationStatus status)
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null) return RedirectToAction("CompleteProfile");

            var application = await _context.JobApplications
                .Include(a => a.Job)
                .Include(a => a.JobSeeker)
                .FirstOrDefaultAsync(a => a.Id == applicationId && a.Job!.EmployerId == employer.Id);
            if (application == null) return NotFound();

            application.Status = status;
            await _context.SaveChangesAsync();

            _context.Notifications.Add(new Notification
            {
                ApplicationUserId = application.JobSeeker!.ApplicationUserId,
                JobId = application.JobId,
                Message = $"Your application for \"{application.Job!.Title}\" is now: {status}.",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return RedirectToAction("Applicants", new { jobId = application.JobId });
        }

        [HttpGet]
        public async Task<IActionResult> ScheduleInterview(int applicationId)
        {
            var employer = await GetCurrentEmployerAsync();
            var application = await _context.JobApplications
                .Include(a => a.Job)
                .Include(a => a.Interview)
                .FirstOrDefaultAsync(a => a.Id == applicationId && a.Job!.EmployerId == employer!.Id);
            if (application == null) return NotFound();

            ViewBag.Application = application;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScheduleInterview(int applicationId, DateTime scheduledAt, string? location, string? notes)
        {
            var employer = await GetCurrentEmployerAsync();
            var application = await _context.JobApplications
                .Include(a => a.Job)
                .Include(a => a.JobSeeker)
                .Include(a => a.Interview)
                .FirstOrDefaultAsync(a => a.Id == applicationId && a.Job!.EmployerId == employer!.Id);
            if (application == null) return NotFound();

            if (application.Interview == null)
            {
                application.Interview = new Interview
                {
                    JobApplicationId = application.Id,
                    Name = $"Interview for {application.Job!.Title}"
                };
                _context.Interviews.Add(application.Interview);
            }

            application.Interview.ScheduledAt = scheduledAt;
            application.Interview.Location = location;
            application.Interview.Notes = notes;
            application.Interview.Status = InterviewStatus.Scheduled;

            await _context.SaveChangesAsync();

            _context.Notifications.Add(new Notification
            {
                ApplicationUserId = application.JobSeeker!.ApplicationUserId,
                JobApplicationId = application.Id,
                Message = $"You've been invited to an interview for \"{application.Job!.Title}\" on {scheduledAt:dd MMM yyyy, h:mm tt}.",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return RedirectToAction("Applicants", new { jobId = application.JobId });
        }

        [HttpGet]
        public async Task<IActionResult> PendingStaff()
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null) return RedirectToAction("CompleteProfile");
            if (employer.Position != EmployerPosition.Owner)
            {
                TempData["Message"] = "Only the company Owner can manage staff requests.";
                return RedirectToAction("Index");
            }

            var pending = await _context.Employers
                .Include(e => e.ApplicationUser)
                .Where(e => e.CompanyId == employer.CompanyId && !e.IsApprovedByOwner && e.Position != EmployerPosition.Owner)
                .ToListAsync();

            var approvedCount = await _context.Employers
                .CountAsync(e => e.BranchId == employer.BranchId && e.IsApprovedByOwner && e.Position != EmployerPosition.Owner);

            ViewBag.ApprovedCount = approvedCount;
            return View(pending);
        }
        // APPROVE STAFF: branch comes from the employee's stored request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveStaff(int employerId)
        {
            var owner = await GetCurrentEmployerAsync();

            if (owner == null ||
                owner.Position != EmployerPosition.Owner)
            {
                return Forbid();
            }

            var staff = await _context.Employers
                .Include(e => e.Branch)
                .FirstOrDefaultAsync(e =>
                    e.Id == employerId &&
                    e.CompanyId == owner.CompanyId &&
                    e.Position != EmployerPosition.Owner);

            if (staff == null)
                return NotFound();

            int branchId = staff.BranchId;

            if (staff.Branch == null || staff.Branch.CompanyId != owner.CompanyId)
                return NotFound();

            if (staff.IsApprovedByOwner)
            {
                TempData["Message"] = "This employee is already approved.";

                return RedirectToAction(
                    "BranchDetails",
                    new { id = branchId });
            }

            var approvedCount = await _context.Employers
                .CountAsync(e =>
                    e.BranchId == branchId &&
                    e.CompanyId == owner.CompanyId &&
                    e.Position != EmployerPosition.Owner &&
                    e.IsApprovedByOwner);

            if (approvedCount >= 3)
            {
                TempData["Message"] =
                    "This branch already has 3 approved employees.";

                return RedirectToAction(
                    "BranchDetails",
                    new { id = branchId });
            }

            staff.IsApprovedByOwner = true;

            await _context.SaveChangesAsync();

            TempData["Message"] =
                $"{staff.JobTitle} was approved for {staff.Branch.Name}.";

            return RedirectToAction(
                "BranchDetails",
                new { id = branchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectStaff(int employerId)
        {
            var owner = await GetCurrentEmployerAsync();

            if (owner == null ||
                owner.Position != EmployerPosition.Owner)
            {
                return Forbid();
            }

            var staff = await _context.Employers
                .FirstOrDefaultAsync(e =>
                    e.Id == employerId &&
                    e.CompanyId == owner.CompanyId &&
                    e.Position != EmployerPosition.Owner &&
                    !e.IsApprovedByOwner);

            if (staff == null)
                return NotFound();

            int branchId = staff.BranchId;

            _context.Employers.Remove(staff);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Employee request rejected.";

            return RedirectToAction(
                "BranchDetails",
                new { id = branchId });
        }

        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null) return RedirectToAction("CompleteProfile");

            var userId = _userManager.GetUserId(User);

            var notifications = await _context.Notifications
                .Include(n => n.JobApplication)
                .Where(n => n.ApplicationUserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            foreach (var n in notifications) n.IsRead = true;
            await _context.SaveChangesAsync();

            return View(notifications);
        }
    }
}
