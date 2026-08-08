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

        public EmployerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var employer = await GetCurrentEmployerAsync();
            if (employer == null) return RedirectToAction("CompleteProfile");

            var jobs = await _context.Jobs
                .Where(j => j.EmployerId == employer.Id)
                .OrderByDescending(j => j.PostedAt)
                .ToListAsync();

            return View(jobs);
        }

        [HttpGet]
        public IActionResult CreateJob()
        {
            return View(new JobViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateJob(JobViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var employer = await GetCurrentEmployerAsync();
            if (employer == null) return RedirectToAction("CompleteProfile");

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
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> EditJob(int id)
        {
            var employer = await GetCurrentEmployerAsync();
            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.EmployerId == employer!.Id);
            if (job == null) return NotFound();

            var model = new JobViewModel
            {
                Id = job.Id,
                Title = job.Title,
                Description = job.Description,
                Location = job.Location,
                SalaryRange = job.SalaryRange
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditJob(JobViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var employer = await GetCurrentEmployerAsync();
            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == model.Id && j.EmployerId == employer!.Id);
            if (job == null) return NotFound();

            job.Title = model.Title;
            job.Description = model.Description;
            job.Location = model.Location;
            job.SalaryRange = model.SalaryRange;

            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var employer = await GetCurrentEmployerAsync();
            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.EmployerId == employer!.Id);
            if (job == null) return NotFound();

            job.Status = job.Status == JobStatus.Open ? JobStatus.Closed : JobStatus.Open;
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult CompleteProfile()
        {
            return View(new CompleteEmployerProfileViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteProfile(CompleteEmployerProfileViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var userId = _userManager.GetUserId(User);

            // Safety check: don't let someone who already has an Employer profile create a second one
            var existing = await _context.Employers.FirstOrDefaultAsync(e => e.ApplicationUserId == userId);
            if (existing != null) return RedirectToAction("Index");

            var company = new Company
            {
                Name = model.CompanyName,
                Website = model.Website,
                IsApproved = false   // Admin must approve before jobs are visible — matches the spec
            };
            _context.Companies.Add(company);
            await _context.SaveChangesAsync();   // saved now so company.Id is generated for the next step

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

        private async Task<Employer?> GetCurrentEmployerAsync()
        {
            var userId = _userManager.GetUserId(User);
            return await _context.Employers.FirstOrDefaultAsync(e => e.ApplicationUserId == userId);
        }
    }
}