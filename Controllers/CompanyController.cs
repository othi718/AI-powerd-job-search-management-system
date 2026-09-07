using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AI_powerd_job_search_management_system.Data;
using AI_powerd_job_search_management_system.Models;
using AI_powerd_job_search_management_system.ViewModels;


namespace AI_powerd_job_search_management_system.Controllers
{
    public class CompanyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public CompanyController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        // PUBLIC: COMPANY DETAILS & INSIGHTS PAGE
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Id == id && c.IsApproved);

            if (company == null) return NotFound();

            var jobs = await _context.Jobs
                .Include(j => j.Employer)
                .Where(j => j.Employer!.CompanyId == id && j.Status == JobStatus.Open)
                .OrderByDescending(j => j.PostedAt)
                .ToListAsync();

            var employees = await _context.Employers
                .Include(e => e.ApplicationUser)
                .Where(e => e.CompanyId == id)
                .ToListAsync();

            var viewModel = new CompanyViewModel
            {
                Company = company,
                Jobs = jobs,
                Employees = employees
            };

            return View(viewModel);
        }

        // COMPANY OWNER: ADD EMPLOYEE
        [Authorize(Roles = "CompanyAdmin,Employer")]
        [HttpGet]
        public IActionResult AddEmployee() => View();

        [Authorize(Roles = "CompanyAdmin,Employer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEmployee(AddEmployeeViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var currentUserId = _userManager.GetUserId(User);
            var currentEmployer = await _context.Employers
                .Include(e => e.Company)
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            if (currentEmployer == null || !currentEmployer.Company!.IsApproved)
            {
                ModelState.AddModelError("", "Your company must be approved by an Admin before assigning employees.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Employer");

                var employer = new Employer
                {
                    ApplicationUserId = user.Id,
                    CompanyId = currentEmployer.CompanyId,
                    JobTitle = model.JobTitle
                };

                _context.Employers.Add(employer);
                await _context.SaveChangesAsync();

                TempData["Message"] = $"Employee {model.FullName} assigned successfully.";
                return RedirectToAction("Details", new { id = currentEmployer.CompanyId });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        // ADMIN: APPROVE COMPANY
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveCompany(int id)
        {
            var company = await _context.Companies.FindAsync(id);
            if (company == null) return NotFound();

            company.IsApproved = true;
            await _context.SaveChangesAsync();

            TempData["Message"] = $"{company.Name} has been approved.";
            return RedirectToAction("Companies", "Admin");
        }
    }
}