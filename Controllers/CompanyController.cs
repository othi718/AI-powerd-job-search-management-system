using AI_powerd_job_search_management_system.Data;
using AI_powerd_job_search_management_system.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AI_powerd_job_search_management_system.Controllers
{
    public class CompanyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CompanyController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == id && c.IsApproved);
            if (company == null) return NotFound();

            var jobs = await _context.Jobs
                .Where(j => j.Employer!.CompanyId == id && j.Status == JobStatus.Open)
                .Include(j => j.Employer)
                .OrderByDescending(j => j.PostedAt)
                .ToListAsync();

            var jobIds = jobs.Select(j => j.Id).ToList();

            var applications = await _context.JobApplications
                .Where(a => jobIds.Contains(a.JobId))
                .ToListAsync();

            double avgMatchScore = applications.Any() ? Math.Round(applications.Average(a => a.MatchScore), 1) : 0;

            var topSkills = await _context.JobSkills
                .Where(js => jobIds.Contains(js.JobId))
                .Include(js => js.Skill)
                .GroupBy(js => js.Skill!.Name)
                .Select(g => new { Skill = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(5)
                .ToListAsync();

            var reviews = await _context.CompanyReviews
                .Include(r => r.JobSeekerUser)
                .Where(r => r.CompanyId == id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            var branches = await _context.Branches
    .Where(b => b.CompanyId == id)
    .ToListAsync();
            ViewBag.Branches = branches;
            var followerCount = await _context.CompanyFollows.CountAsync(f => f.CompanyId == id);

            bool isFollowing = false;
            bool hasReviewed = false;
            if (User.Identity?.IsAuthenticated == true && User.IsInRole("JobSeeker"))
            {
                var userId = _userManager.GetUserId(User);
                isFollowing = await _context.CompanyFollows.AnyAsync(f => f.CompanyId == id && f.JobSeekerUserId == userId);
                hasReviewed = await _context.CompanyReviews.AnyAsync(r => r.CompanyId == id && r.JobSeekerUserId == userId);
            }

            ViewBag.Company = company;
            ViewBag.Jobs = jobs;
            ViewBag.AvgMatchScore = avgMatchScore;
            ViewBag.TotalApplications = applications.Count;
            ViewBag.TopSkills = topSkills;
            ViewBag.Reviews = reviews;
            ViewBag.FollowerCount = followerCount;
            ViewBag.IsFollowing = isFollowing;
            ViewBag.HasReviewed = hasReviewed;
            ViewBag.AvgRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0;

            return View();
        }

        [Authorize(Roles = "JobSeeker")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFollow(int companyId)
        {
            var userId = _userManager.GetUserId(User);

            var existing = await _context.CompanyFollows
                .FirstOrDefaultAsync(f => f.CompanyId == companyId && f.JobSeekerUserId == userId);

            if (existing != null)
                _context.CompanyFollows.Remove(existing);
            else
                _context.CompanyFollows.Add(new CompanyFollow { CompanyId = companyId, JobSeekerUserId = userId! });

            await _context.SaveChangesAsync();
            return RedirectToAction("Details", new { id = companyId });
        }

        [Authorize(Roles = "JobSeeker")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int companyId, int rating, string? comment)
        {
            var userId = _userManager.GetUserId(User);

            var existing = await _context.CompanyReviews
                .FirstOrDefaultAsync(r => r.CompanyId == companyId && r.JobSeekerUserId == userId);

            if (existing == null && rating >= 1 && rating <= 5)
            {
                _context.CompanyReviews.Add(new CompanyReview
                {
                    CompanyId = companyId,
                    JobSeekerUserId = userId!,
                    Rating = rating,
                    Comment = comment
                });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Details", new { id = companyId });
        }
    }
}