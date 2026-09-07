using System.Diagnostics;
using AI_powerd_job_search_management_system.Data;
using AI_powerd_job_search_management_system.Models;
using AI_powerd_job_search_management_system.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AI_powerd_job_search_management_system.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Open jobs from employers whose company has been approved by an admin.
            var openApprovedJobs = _context.Jobs
                .Include(j => j.Employer)
                    .ThenInclude(e => e!.Company)
                .Where(j => j.Status == JobStatus.Open && j.Employer!.Company!.IsApproved);

            var vm = new HomeViewModel
            {
                OpenJobsCount = await openApprovedJobs.CountAsync(),
                ApprovedCompaniesCount = await _context.Companies.CountAsync(c => c.IsApproved),
                JobSeekersCount = await _context.JobSeekers.CountAsync(),
                SuccessfulHiresCount = await _context.JobApplications.CountAsync(a => a.Status == AI_Powered_Smart_Job_Management_System.Models.ApplicationStatus.Accepted),

                FeaturedJobs = await openApprovedJobs
                    .OrderByDescending(j => j.PostedAt)
                    .Take(3)
                    .ToListAsync(),

                TopCompanies = await _context.Companies
                    .Where(c => c.IsApproved)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(6)
                    .ToListAsync()
            };

            return View(vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        [Route("Home/StatusCode")]
        public IActionResult StatusCode(int code)
        {
            ViewBag.Code = code;
            return View();
        }
    }
}