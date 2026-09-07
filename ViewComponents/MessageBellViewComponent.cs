using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AI_powerd_job_search_management_system.Data;
using AI_powerd_job_search_management_system.Models;

namespace AI_powerd_job_search_management_system.ViewComponents
{
    public class MessageConversationInfo
    {
        public int JobApplicationId { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string LastMessage { get; set; } = string.Empty;
        public int UnreadCount { get; set; }
    }

    public class MessageBellViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MessageBellViewComponent(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (!(UserClaimsPrincipal?.Identity?.IsAuthenticated ?? false))
                return Content("");

            var userId = _userManager.GetUserId(UserClaimsPrincipal);

            var relevantMessages = await _context.InterviewMessages
                .Include(m => m.JobApplication).ThenInclude(a => a!.Job)
                .Include(m => m.JobApplication).ThenInclude(a => a!.JobSeeker)
                .Include(m => m.JobApplication).ThenInclude(a => a!.Job).ThenInclude(j => j!.Employer)
                .Where(m =>
                    m.SenderUserId != userId &&
                    !m.IsRead &&
                    (m.JobApplication!.JobSeeker!.ApplicationUserId == userId ||
                     m.JobApplication.Job!.Employer!.ApplicationUserId == userId))
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            var conversations = relevantMessages
                .GroupBy(m => m.JobApplicationId)
                .Select(g => new MessageConversationInfo
                {
                    JobApplicationId = g.Key,
                    JobTitle = g.First().JobApplication!.Job!.Title,
                    LastMessage = g.OrderByDescending(m => m.SentAt).First().Message,
                    UnreadCount = g.Count()
                })
                .ToList();

            return View(conversations);
        }
    }
}