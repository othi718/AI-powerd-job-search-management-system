using System.Net.Http.Json;
using System.Text.Json;

namespace AI_powerd_job_search_management_system.Services
{
    public class GeminiService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public GeminiService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _apiKey = config["GeminiSettings:ApiKey"] ?? string.Empty;
        }

        public async Task<string> GenerateMatchInsightAsync(
            string jobTitle, string jobDescription,
            List<string> requiredSkills, List<string> candidateSkills, double matchScore)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                return "AI insight unavailable (no API key configured).";

            var prompt =
                $"You are a recruitment assistant. A candidate applied for the job \"{jobTitle}\".\n" +
                $"Job description: {jobDescription}\n" +
                $"Required skills: {string.Join(", ", requiredSkills)}\n" +
                $"Candidate's skills: {string.Join(", ", candidateSkills)}\n" +
                $"Calculated match score: {matchScore}%.\n" +
                "In 2-3 short sentences, give the employer a brief, honest insight about this candidate's fit " +
                "for the role, mentioning their strongest relevant skill and the biggest gap, if any. " +
                "Be concise and professional.";

            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    }
                };

                var response = await _http.PostAsJsonAsync(url, requestBody);
                if (!response.IsSuccessStatusCode)
                    return "AI insight unavailable at this time.";

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                var text = json
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text?.Trim() ?? "AI insight unavailable.";
            }
            catch
            {
                return "AI insight unavailable at this time.";
            }
        }
    }
}