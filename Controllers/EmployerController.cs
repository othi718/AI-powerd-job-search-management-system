using AI_powerd_job_search_management_system.Data;
using AI_powerd_job_search_management_system.Models;
using AI_powerd_job_search_management_system.Utilities;
using AI_powerd_job_search_management_system.ViewModels;
using AI_Powered_Smart_Job_Management_System.Models;
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
            if (employer == null) return RedirectToAction("CompleteProfile");

            var jobsQuery = _context.Jobs
                .Include(j => j.Applications)
                .Where(j => j.EmployerId == employer.Id);

            if (!string.IsNullOrWhiteSpace(search))
                jobsQuery = jobsQuery.Where(j => j.Title.Contains(search));

            if (status == "Open")
                jobsQuery = jobsQuery.Where(j => j.Status == JobStatus.Open);
            else if (status == "Closed")
                jobsQuery = jobsQuery.Where(j => j.Status == JobStatus.Closed);

            var jobs = await jobsQuery.OrderByDescending(j => j.PostedAt).ToListAsync();

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

            return View(new JobViewModel());
        }



        // CREATE JOB - POST

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateJob(JobViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var employer = await GetCurrentEmployerAsync();
            if (employer == null)
                return RedirectToAction("CompleteProfile");

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

            // ... everything else in this method stays exactly the same

            // Get logged-in employer



            if (employer == null)
                return RedirectToAction("CompleteProfile");



            // 1. CREATE JOB


            var job = new Job
            {
                EmployerId = employer.Id,
                Title = model.Title,
                Description = model.Description,
                Location = model.Location,
                SalaryRange = model.SalaryRange,
                Category = model.Category,
                Deadline = model.Deadline,
                Status = JobStatus.Open
            };

            _context.Jobs.Add(job);

            // Save first so Job.Id is generated
            await _context.SaveChangesAsync();


     
            // 2. GET REQUIRED SKILLS
          

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


            // 3. CREATE JOB SKILLS
         

            foreach (var skillName in requiredSkillNames)
            {
                // Check if skill already exists
                var skill = await _context.Skills
                    .FirstOrDefaultAsync(s =>
                        s.Name.ToLower() ==
                        skillName.ToLower());


               
                // If skill doesn't exist, create it
               
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


                // Check duplicate JobSkill
               
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


            // 4. LOAD JOB REQUIRED SKILLS
        

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


                // Find matching skills
            

                var matchingSkills = requiredSkills
      .Where(required => candidateSkills.Any(candidate => SkillMatcher.IsMatch(candidate, required)))
      .ToList();

                
                // Find missing skills
               

                var missingSkills = requiredSkills
      .Where(required => !candidateSkills.Any(candidate => SkillMatcher.IsMatch(candidate, required)))
      .ToList();


                // 7. CALCULATE MATCH PERCENTAGE
           

                int matchPercentage = 0;

                if (requiredSkills.Count > 0)
                {
                    matchPercentage =
                        (int)Math.Round(
                            (double)matchingSkills.Count /
                            requiredSkills.Count *
                            100);
                }


             
                // 8. CREATE NOTIFICATION
              

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
                        JobId = job.Id,

                        Message = message,

                        IsRead = false,

                        CreatedAt = DateTime.UtcNow
                    };


                    _context.Notifications.Add(notification);

                    notificationCount++;
                }
            }


            
     

            await _context.SaveChangesAsync();


           
            // 10. SHOW RESULT TO EMPLOYER
        

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


      
        // EDIT JOB - GET
      
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
                SalaryRange = job.SalaryRange,
                Category = job.Category,
                Deadline = job.Deadline

            };
            model.RequiredSkills = string.Join(", ", await _context.JobSkills
    .Where(js => js.JobId == job.Id)
    .Include(js => js.Skill)
    .Select(js => js.Skill!.Name)
    .ToListAsync());



            return View(model);
        }


        // EDIT JOB - POST

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
            job.Category = model.Category;
            job.Deadline = model.Deadline;


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



        // COMPLETE EMPLOYER PROFILE - GET

        [HttpGet]
        public IActionResult CompleteProfile()
        {
            return View();
        }

        // COMPLETE EMPLOYER PROFILE - POST

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

        [HttpGet]
        public async Task<IActionResult> CompleteProfileStaff()
        {
            ViewBag.Companies = await _context.Companies
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();
            return View(new JoinCompanyViewModel());
        }
        //completeProfileStaff -post

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteProfileStaff(JoinCompanyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Companies = await _context.Companies.OrderBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync();
                return View(model);
            }
            var userId = _userManager.GetUserId(User);

            var existingEmployer = await _context.Employers.FirstOrDefaultAsync(e => e.ApplicationUserId == userId);
            if (existingEmployer != null)
            {
                TempData["Message"] = "You already have a company profile set up.";
                return RedirectToAction("Index");
            }

            var company = await _context.Companies.Include(c => c.Branches).FirstOrDefaultAsync(c => c.Id == model.CompanyId);
            if (company == null)
            {
                ModelState.AddModelError("", "Selected company not found.");
                ViewBag.Companies = await _context.Companies.OrderBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync();
                return View(model);
            }

            var mainBranch = company.Branches.FirstOrDefault();
            if (mainBranch == null)
            {
                mainBranch = new Branch { CompanyId = company.Id, Name = "Main Branch" };
                _context.Branches.Add(mainBranch);
                await _context.SaveChangesAsync();
            }

            var employer = new Employer
            {
                ApplicationUserId = userId!,
                CompanyId = company.Id,
                BranchId = mainBranch.Id,
                JobTitle = model.JobTitle,
                Position = model.Position,
                IsApprovedByOwner = false
            };
            _context.Employers.Add(employer);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Your request to join has been sent to the company owner for approval.";
            return RedirectToAction("Index");
        }


        // GET CURRENT EMPLOYER
        private async Task<Employer?> GetCurrentEmployerAsync()
        {
            var userId = _userManager.GetUserId(User);


            return await _context.Employers
                .FirstOrDefaultAsync(
                    e => e.ApplicationUserId == userId);
        }


        // APPLICANTS

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveStaff(int employerId)
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null || employer.Position != EmployerPosition.Owner) return Forbid();

            var staff = await _context.Employers.FirstOrDefaultAsync(e => e.Id == employerId && e.CompanyId == employer.CompanyId);
            if (staff == null) return NotFound();

            var approvedCount = await _context.Employers
                .CountAsync(e => e.BranchId == staff.BranchId && e.IsApprovedByOwner && e.Position != EmployerPosition.Owner);

            if (approvedCount >= 3)
            {
                TempData["Message"] = "This branch already has 3 approved staff members. Cannot approve more.";
                return RedirectToAction("PendingStaff");
            }

            staff.IsApprovedByOwner = true;
            await _context.SaveChangesAsync();

            TempData["Message"] = "Staff member approved.";
            return RedirectToAction("PendingStaff");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectStaff(int employerId)
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null || employer.Position != EmployerPosition.Owner) return Forbid();

            var staff = await _context.Employers.FirstOrDefaultAsync(e => e.Id == employerId && e.CompanyId == employer.CompanyId);
            if (staff == null) return NotFound();

            _context.Employers.Remove(staff);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Request rejected.";
            return RedirectToAction("PendingStaff");
        }

    }
}