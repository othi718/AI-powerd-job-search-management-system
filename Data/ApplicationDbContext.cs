using AI_powerd_job_search_management_system.Models;
using AI_Powered_Smart_Job_Management_System.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace AI_powerd_job_search_management_system.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Company> Companies { get; set; }
        public DbSet<Employer> Employers { get; set; }
        public DbSet<JobSeeker> JobSeekers { get; set; }
        public DbSet<Resume> Resumes { get; set; }
        public DbSet<Education> Educations { get; set; }
        public DbSet<Experience> Experiences { get; set; }
        public DbSet<Certification> Certifications { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ExtracurricularActivity> ExtracurricularActivities { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<CandidateSkill> CandidateSkills { get; set; }
        public DbSet<JobSkill> JobSkills { get; set; }
        public DbSet<Job> Jobs { get; set; }
        public DbSet<JobApplication> JobApplications { get; set; }
        public DbSet<AIAnalysis> AIAnalyses { get; set; }
        public DbSet<SavedJob> SavedJobs { get; set; }
        public DbSet<Interview> Interviews { get; set; }
        public DbSet<InterviewMessage> InterviewMessages { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<JobSeekerSkill> JobSeekerSkills { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<CompanyFollow> CompanyFollows { get; set; }
        public DbSet<CompanyReview> CompanyReviews { get; set; }



        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // JobSeeker -> JobApplication
            builder.Entity<JobApplication>()
                .HasOne(ja => ja.JobSeeker)
                .WithMany(js => js.Applications)
                .HasForeignKey(ja => ja.JobSeekerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Resume -> JobApplication
            builder.Entity<JobApplication>()
                .HasOne(ja => ja.Resume)
                .WithMany()
                .HasForeignKey(ja => ja.ResumeId)
                .OnDelete(DeleteBehavior.Restrict);

            // JobSeeker -> SavedJob
            builder.Entity<SavedJob>()
                .HasOne(sj => sj.JobSeeker)
                .WithMany(js => js.SavedJobs)
                .HasForeignKey(sj => sj.JobSeekerId)
                .OnDelete(DeleteBehavior.Restrict);

            // ApplicationUser -> InterviewMessage
            builder.Entity<InterviewMessage>()
                .HasOne(im => im.Sender)
                .WithMany()
                .HasForeignKey(im => im.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // IMPORTANT:
            // Prevent Company -> Branch -> Employer
            // and Company -> Employer multiple cascade paths
            builder.Entity<Employer>()
    .HasOne(e => e.Branch)
    .WithMany(b => b.Employers)
    .HasForeignKey(e => e.BranchId)
    .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Employer>()
    .HasOne(e => e.Company)
    .WithMany(c => c.Employers)
    .HasForeignKey(e => e.CompanyId)
    .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<Branch>()
    .HasOne(b => b.Company)
    .WithMany(c => c.Branches)
    .HasForeignKey(b => b.CompanyId)
    .OnDelete(DeleteBehavior.Cascade);
            // Unique Company Follow
            builder.Entity<CompanyFollow>()
                .HasIndex(f => new { f.JobSeekerUserId, f.CompanyId })
                .IsUnique();

            // Unique Company Review
            builder.Entity<CompanyReview>()
                .HasIndex(r => new { r.JobSeekerUserId, r.CompanyId })
                .IsUnique();
        }
    }
}