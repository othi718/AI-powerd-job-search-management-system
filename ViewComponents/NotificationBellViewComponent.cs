using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AI_powerd_job_search_management_system.Data;
using AI_powerd_job_search_management_system.Models;

namespace AI_powerd_job_search_management_system.ViewComponents
{
    public class NotificationBellViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationBellViewComponent(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (!(UserClaimsPrincipal?.Identity?.IsAuthenticated ?? false))
                return Content("");

            var userId = _userManager.GetUserId(UserClaimsPrincipal);

            int unreadCount = await _context.Notifications
                .CountAsync(n => n.ApplicationUserId == userId && !n.IsRead);

            return View(unreadCount);
        }
    }
}