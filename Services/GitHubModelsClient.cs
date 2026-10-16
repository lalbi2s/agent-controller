using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace AgentController.Services;
public class GitHubModelsClient : ILLMClient
{
    private readonly HttpClient _http;
    private readonly string _token;

    public string ProviderName => "github";

    /// <summary>
    /// False when the token is missing or is obviously a placeholder (e.g. "ghp_").
    /// The client is still registered so the factory can report it clearly in logs.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// GitHub Models API rejects HTTP bodies over ~64 KB.
    /// 20 000 chars leaves comfortable room for the system prompt + JSON encoding overhead.
    /// </summary>
    public int MaxInputChars => 20_000;

    public GitHubModelsClient(IConfiguration config, HttpClient http)
    {
        _http = http;
        _token = config["GitHub:Token"] ?? string.Empty;

        // Consider available only when the token looks non-trivial (> 10 chars)
        IsAvailable = _token.Length > 10;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        string model,
        CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            "https://models.inference.ai.azure.com/chat/completions");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        var body = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userMessage  }
            },
            max_tokens = 16000
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);

        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}