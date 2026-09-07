using AI_powerd_job_search_management_system.Data;
using AI_powerd_job_search_management_system.Models;
using AI_Powered_Smart_Job_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AI_powerd_job_search_management_system.Controllers
{
    [Authorize]
    public class MessagesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MessagesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<JobApplication?> GetAuthorizedApplicationAsync(int applicationId)
        {
            var userId = _userManager.GetUserId(User);

            return await _context.JobApplications
                .Include(a => a.Job).ThenInclude(j => j!.Employer)
                .Include(a => a.JobSeeker)
                .Include(a => a.Interview)
                .FirstOrDefaultAsync(a =>
                    a.Id == applicationId &&
                    (a.JobSeeker!.ApplicationUserId == userId || a.Job!.Employer!.ApplicationUserId == userId));
        }

        // Overview page: interview details + a link to the chat
        [HttpGet]
        public async Task<IActionResult> Discuss(int applicationId)
        {
            var application = await GetAuthorizedApplicationAsync(applicationId);
            if (application == null) return Forbid();

            var userId = _userManager.GetUserId(User);
            int unreadCount = await _context.InterviewMessages
                .CountAsync(m => m.JobApplicationId == applicationId && m.SenderUserId != userId && !m.IsRead);

            ViewBag.Application = application;
            ViewBag.UnreadCount = unreadCount;
            return View();
        }

        // Actual chat thread
        [HttpGet]
        public async Task<IActionResult> Chat(int applicationId)
        {
            var application = await GetAuthorizedApplicationAsync(applicationId);
            if (application == null) return Forbid();

            var userId = _userManager.GetUserId(User);

            var messages = await _context.InterviewMessages
                .Include(m => m.Sender)
                .Where(m => m.JobApplicationId == applicationId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            // Mark messages sent by the other person as read now that this user is viewing them
            var unread = messages.Where(m => m.SenderUserId != userId && !m.IsRead).ToList();
            foreach (var m in unread) m.IsRead = true;
            if (unread.Any()) await _context.SaveChangesAsync();

            ViewBag.Application = application;
            ViewBag.CurrentUserId = userId;
            return View(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(int applicationId, string message)
        {
            var application = await GetAuthorizedApplicationAsync(applicationId);
            if (application == null) return Forbid();

            if (!string.IsNullOrWhiteSpace(message))
            {
                _context.InterviewMessages.Add(new InterviewMessage
                {
                    JobApplicationId = applicationId,
                    SenderUserId = _userManager.GetUserId(User)!,
                    Message = message.Trim(),
                    IsRead = false
                });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Chat", new { applicationId });
        }
    }
}