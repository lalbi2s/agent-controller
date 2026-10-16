using System.Text;
using System.Text.Json;

namespace AgentController.Services;

public class AnthropicClient : ILLMClient
{
    private readonly HttpClient _http;
    private readonly string _defaultModel;
    private readonly ILogger<AnthropicClient> _logger;

    public string ProviderName => "anthropic";
    public bool IsAvailable { get; }
    public int MaxInputChars => 100_000;

    public AnthropicClient(HttpClient httpClient, IConfiguration config, ILogger<AnthropicClient> logger)
    {
        _http = httpClient;
        _logger = logger;
        _defaultModel = config["Anthropic:DefaultModel"] ?? "claude-sonnet-4-20250514";

        var apiKey = config["Anthropic:ApiKey"];
        IsAvailable = !string.IsNullOrWhiteSpace(apiKey);

        _http.BaseAddress = new Uri("https://api.anthropic.com");
        _http.Timeout = TimeSpan.FromMinutes(3);
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey ?? string.Empty);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, string model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model)) model = _defaultModel;
        _logger.LogDebug("Calling Anthropic API with model {Model}", model);

        var request = new
        {
            model,
            max_tokens = 16384,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userMessage } }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("/v1/messages", content, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic API error: {StatusCode} {Body}", response.StatusCode, responseBody);
            throw new HttpRequestException($"Anthropic API error ({response.StatusCode}): {responseBody}");
        }

        var doc = JsonDocument.Parse(responseBody);
        var textBlock = doc.RootElement
            .GetProperty("content")
            .EnumerateArray()
            .FirstOrDefault(b => b.GetProperty("type").GetString() == "text");

        return textBlock.GetProperty("text").GetString() ?? string.Empty;
    }
}