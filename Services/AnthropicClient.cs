using System.Text;
using System.Text.Json;

namespace AgentController.Services;

public class AnthropicClient : ILLMClient
{
    private const string BaseUrl = "https://api.anthropic.com";

    private readonly IHttpClientFactory _httpFactory;
    private readonly ApiKeyResolver _keys;
    private readonly string _defaultModel;

    public string ProviderName => "anthropic";
    public bool IsAvailable => _keys.GetKey(ProviderName) is not null;
    public int MaxInputChars => 100_000;

    public AnthropicClient(IHttpClientFactory httpFactory, ApiKeyResolver keys, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _keys = keys;
        _defaultModel = config["Anthropic:DefaultModel"] ?? "claude-sonnet-5-5";
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, string model, CancellationToken ct = default)
    {
        var body = new
        {
            model = string.IsNullOrWhiteSpace(model) ? _defaultModel : model,
            max_tokens = 16384,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userMessage } }
        };

        var apiKey = RequireKey();
        using var request = CreateRequest(HttpMethod.Post, "/v1/messages", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await SendAsync(request, ct);
        await LlmHttp.EnsureSuccessAsync(response, "Anthropic", apiKey, ct);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        if (root.TryGetProperty("stop_reason", out var stopReason) && stopReason.GetString() == "max_tokens")
            throw new InvalidOperationException("The model response was cut off because it reached the output limit");

        var textBlock = root.GetProperty("content")
            .EnumerateArray()
            .FirstOrDefault(block => block.GetProperty("type").GetString() == "text");

        return textBlock.ValueKind == JsonValueKind.Undefined
            ? string.Empty
            : textBlock.GetProperty("text").GetString() ?? string.Empty;
    }

    public async Task TestConnectionAsync(CancellationToken ct = default)
    {
        var apiKey = RequireKey();
        using var request = CreateRequest(HttpMethod.Get, "/v1/models?limit=1", apiKey);

        using var response = await SendAsync(request, ct);
        await LlmHttp.EnsureSuccessAsync(response, "Anthropic", apiKey, ct);
    }

    private string RequireKey() =>
        _keys.GetKey(ProviderName) ?? throw new InvalidOperationException("No Anthropic API key is configured");

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string apiKey)
    {
        var request = new HttpRequestMessage(method, BaseUrl + path);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // A new HttpClient per call is cheap: the factory reuses the connections
        using var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(3);
        return await http.SendAsync(request, ct);
    }
}
