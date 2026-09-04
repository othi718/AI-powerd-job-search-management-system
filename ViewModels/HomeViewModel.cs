using AI_powerd_job_search_management_system.Models;

namespace AI_powerd_job_search_management_system.ViewModels
{
    public class HomeViewModel
    {
        public int OpenJobsCount { get; set; }
        public int ApprovedCompaniesCount { get; set; }
        public int JobSeekersCount { get; set; }
        public int SuccessfulHiresCount { get; set; }

        public List<Job> FeaturedJobs { get; set; } = new();
        public List<Company> TopCompanies { get; set; } = new();
    }
}