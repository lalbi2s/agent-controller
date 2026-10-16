using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AgentController.Services;

/// <summary>Any server that implements the OpenAI chat completions API (OpenAI, Azure, Ollama...).</summary>
public class OpenAIClient : ILLMClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ApiKeyResolver _keys;
    private readonly string _baseUrl;
    private readonly string _defaultModel;

    public string ProviderName => "openai";
    public bool IsAvailable => _keys.GetKey(ProviderName) is not null;
    public int MaxInputChars => 100_000;

    public OpenAIClient(IHttpClientFactory httpFactory, ApiKeyResolver keys, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _keys = keys;
        _baseUrl = (config["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1").TrimEnd('/');
        _defaultModel = config["OpenAI:DefaultModel"] ?? "gpt-4o-mini";
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, string model, CancellationToken ct = default)
    {
        var body = new
        {
            model = string.IsNullOrWhiteSpace(model) ? _defaultModel : model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            }
        };

        var apiKey = RequireKey();
        using var request = CreateRequest(HttpMethod.Post, "/chat/completions", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await SendAsync(request, ct);
        await LlmHttp.EnsureSuccessAsync(response, "OpenAI", apiKey, ct);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var choice = doc.RootElement.GetProperty("choices")[0];

        if (choice.TryGetProperty("finish_reason", out var reason) && reason.GetString() == "length")
            throw new InvalidOperationException("The model response was cut off because it reached the output limit");

        return choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    public async Task TestConnectionAsync(CancellationToken ct = default)
    {
        var apiKey = RequireKey();
        using var request = CreateRequest(HttpMethod.Get, "/models", apiKey);

        using var response = await SendAsync(request, ct);
        await LlmHttp.EnsureSuccessAsync(response, "OpenAI", apiKey, ct);
    }

    private string RequireKey() =>
        _keys.GetKey(ProviderName) ?? throw new InvalidOperationException("No OpenAI API key is configured");

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string apiKey)
    {
        var request = new HttpRequestMessage(method, _baseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(3);
        return await http.SendAsync(request, ct);
    }
}
