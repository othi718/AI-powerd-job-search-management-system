using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_powerd_job_search_management_system.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        public string ApplicationUserId { get; set; } = string.Empty;
        [ForeignKey(nameof(ApplicationUserId))]
        public ApplicationUser? ApplicationUser { get; set; }

        [Required]
        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}