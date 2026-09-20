using System.Net.Http.Json;
using System.Text.Json;

namespace AI_powerd_job_search_management_system.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeminiService> _logger;

        public GeminiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<GeminiService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> GenerateMatchInsightAsync(
            string jobTitle,
            string jobDescription,
            List<string> requiredSkills,
            List<string> candidateSkills,
            double matchScore)
        {
            var apiKey = _configuration["GeminiSettings:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError(
                    "Gemini API key was not found at GeminiSettings:ApiKey.");

                return "AI insight unavailable: API key is not configured.";
            }

            var prompt =
                $"You are a professional recruitment assistant.\n\n" +
                $"Job title: {jobTitle}\n" +
                $"Job description: {jobDescription}\n" +
                $"Required skills: " +
                $"{(requiredSkills.Any() ? string.Join(", ", requiredSkills) : "Not specified")}\n" +
                $"Candidate skills: " +
                $"{(candidateSkills.Any() ? string.Join(", ", candidateSkills) : "Not specified")}\n" +
                $"Calculated match score: {matchScore:F1}%\n\n" +
                "Write two short professional sentences about the candidate's suitability. " +
                "Mention the strongest matching skill and the most important missing skill. " +
                "Do not change the calculated match score.";

            try
            {
                // Current Gemini Flash model.
                const string url =
                    "https://generativelanguage.googleapis.com/v1beta/" +
                    "models/gemini-3.8-flash:generateContent";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[]
                            {
                                new
                                {
                                    text = prompt
                                }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.4,
                        maxOutputTokens = 200
                    }
                };

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    url);

                request.Headers.Add("x-goog-api-key", apiKey);
                request.Content = JsonContent.Create(requestBody);

                using var response = await _httpClient.SendAsync(request);

                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Gemini request failed. Status: {StatusCode}. Response: {Response}",
                        (int)response.StatusCode,
                        responseBody);

                    return $"AI insight unavailable. Gemini returned HTTP {(int)response.StatusCode}.";
                }

                using var jsonDocument = JsonDocument.Parse(responseBody);
                var root = jsonDocument.RootElement;

                if (!root.TryGetProperty("candidates", out var candidates) ||
                    candidates.GetArrayLength() == 0)
                {
                    _logger.LogError(
                        "Gemini response did not contain candidates. Response: {Response}",
                        responseBody);

                    return "AI insight unavailable: Gemini returned no candidate response.";
                }

                var firstCandidate = candidates[0];

                if (!firstCandidate.TryGetProperty("content", out var content) ||
                    !content.TryGetProperty("parts", out var parts) ||
                    parts.GetArrayLength() == 0 ||
                    !parts[0].TryGetProperty("text", out var textElement))
                {
                    _logger.LogError(
                        "Gemini response did not contain generated text. Response: {Response}",
                        responseBody);

                    return "AI insight unavailable: Gemini returned no text.";
                }

                var insight = textElement.GetString();

                if (string.IsNullOrWhiteSpace(insight))
                {
                    return "AI insight unavailable: Gemini returned empty text.";
                }

                return insight.Trim();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(
                    ex,
                    "Network error while communicating with Gemini.");

                return "AI insight unavailable: could not connect to Gemini.";
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "Gemini returned an invalid JSON response.");

                return "AI insight unavailable: invalid Gemini response.";
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error while generating Gemini insight.");

                return "AI insight unavailable due to an unexpected error.";
            }
        }
    }
}