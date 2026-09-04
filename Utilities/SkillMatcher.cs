namespace AI_powerd_job_search_management_system.Utilities
{
    public static class SkillMatcher
    {
        // Returns true if two skill names are considered a match:
        // either identical, or one contains the other (handles
        // formatting variants like "SQL" vs "SQL Server").
        public static bool IsMatch(string a, string b)
        {
            a = a.Trim();
            b = b.Trim();

            return a.Contains(b, StringComparison.OrdinalIgnoreCase) ||
                   b.Contains(a, StringComparison.OrdinalIgnoreCase);
        }
    }
}
