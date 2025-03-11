using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Interfaces;

namespace Infrastructure.Summarizers;

public class ClaudeSummarizer : ISummarizer
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public ClaudeSummarizer(string apiKey, HttpClient httpClient)
    {
        _apiKey = apiKey;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.anthropic.com/v1/");
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<string> SummarizeAsync(string content, string subject)
    {
        var prompt = $@"Summarize the following email in English, formatting the result as a Markdown document.

Format requirements:
1. Start with a level 1 header containing the email subject
2. Add the date as a level 2 header
3. Present main points as a bullet list
4. Use appropriate Markdown formatting for highlighting important elements (bold, italic, etc.)
5. If there are any links or references, preserve them in Markdown format

Email subject: '{subject}'

Content:
{content}";

        var request = new
        {
            model = "claude-3-opus-20240229",
            max_tokens = 1000,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            }
        };

        var response = await _httpClient.PostAsync(
            "messages",
            new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
        );

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Claude API error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var responseObject = JsonSerializer.Deserialize<ClaudeResponse>(responseContent, options);

        if (responseObject?.Content == null || responseObject.Content.Length == 0)
        {
            return "Failed to generate summary.";
        }

        return responseObject.Content[0].Text;
    }

    private class ClaudeResponse
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required Message[] Content { get; set; }

        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("usage")]
        public required Usage Usage { get; set; }
    }

    private class Message
    {
        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("text")]
        public required string Text { get; set; }
    }

    private class Usage
    {
        [JsonPropertyName("input_tokens")]
        public required int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public required int OutputTokens { get; set; }
    }
} 