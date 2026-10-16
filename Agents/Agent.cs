using System.Net;
using System.Text.Json;
using AgentController.Services;

namespace AgentController.Agents;

public class Agent
{
    private readonly LLMClientFactory _clients;
    private readonly string _provider;
    private readonly string _model;
    private readonly ILogger _logger;
    private string _systemPrompt;

    public string Name { get; }
    public string Role { get; }

    public Agent(
        string name,
        string role,
        string systemPrompt,
        LLMClientFactory clients,
        string provider,
        string model,
        ILogger logger)
    {
        Name = name;
        Role = role;
        _systemPrompt = systemPrompt;
        _clients = clients;
        _provider = provider;
        _model = model;
        _logger = logger;
    }

    public void SetSystemPrompt(string prompt) => _systemPrompt = prompt;

    /// <summary>Largest input (chars) the provider of this agent accepts in one request.</summary>
    public int GetMaxInputChars() => _clients.GetClient(_provider).MaxInputChars;

    /// <summary>
    /// Runs the agent. With <paramref name="expectJson"/>, the answer must contain valid JSON:
    /// the request is repeated once if it does not.
    /// </summary>
    public async Task<string> ExecuteAsync(string input, bool expectJson, CancellationToken ct = default)
    {
        _logger.LogInformation("[{Agent}] Executing...", Name);

        var raw = await CompleteAsync(input, ct);
        if (!expectJson)
            return raw.Trim();

        var json = ExtractJson(raw);
        if (json is not null)
            return json;

        _logger.LogWarning("[{Agent}] The answer is not valid JSON, retrying once", Name);
        ct.ThrowIfCancellationRequested();

        raw = await CompleteAsync(input, ct);
        return ExtractJson(raw) ?? raw.Trim();
    }

    private async Task<string> CompleteAsync(string input, CancellationToken ct)
    {
        var client = _clients.GetClient(_provider);

        // The configured model belongs to the configured provider. A substitute provider uses its own default.
        var model = client.ProviderName.Equals(_provider, StringComparison.OrdinalIgnoreCase) ? _model : "";

        try
        {
            return await client.CompleteAsync(_systemPrompt, input, model, ct);
        }
        catch (HttpRequestException ex) when (ShouldTryAnotherProvider(ex))
        {
            var fallback = _clients.GetFallbackClient(client.ProviderName)
                ?? throw new InvalidOperationException(
                    $"Provider '{client.ProviderName}' failed ({ex.Message}) and no other provider is available");

            _logger.LogWarning("[{Agent}] Provider {Provider} failed with HTTP {Status}, trying {Fallback}",
                Name, client.ProviderName, (int?)ex.StatusCode, fallback.ProviderName);

            return await fallback.CompleteAsync(_systemPrompt, input, "", ct);
        }
    }

    // Rejected key, or request too large for this provider
    private static bool ShouldTryAnotherProvider(HttpRequestException ex) =>
        ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.RequestEntityTooLarge;

    /// <summary>Finds the JSON value in a model answer, which may be wrapped in text or a markdown fence.</summary>
    public static string? ExtractJson(string text)
    {
        text = text.Trim();
        if (IsValidJson(text))
            return text;

        foreach (var (open, close) in new[] { ('{', '}'), ('[', ']') })
        {
            int start = text.IndexOf(open);
            int end = text.LastIndexOf(close);
            if (start < 0 || end <= start)
                continue;

            var candidate = text[start..(end + 1)];
            if (IsValidJson(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsValidJson(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
