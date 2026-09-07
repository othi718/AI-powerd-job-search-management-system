using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AI_powerd_job_search_management_system.Data;
using AI_powerd_job_search_management_system.Models;

namespace AI_powerd_job_search_management_system.ViewComponents
{
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

            int unreadCount = await _context.InterviewMessages
                .Include(m => m.JobApplication).ThenInclude(a => a!.Job).ThenInclude(j => j!.Employer)
                .Include(m => m.JobApplication).ThenInclude(a => a!.JobSeeker)
                .CountAsync(m =>
                    m.SenderUserId != userId &&
                    !m.IsRead &&
                    (m.JobApplication!.JobSeeker!.ApplicationUserId == userId ||
                     m.JobApplication.Job!.Employer!.ApplicationUserId == userId));

            return View(unreadCount);
        }
    }
}