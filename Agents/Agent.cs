using System.Net;
using AgentController.Services;

namespace AgentController.Agents;

public class Agent
{
    private readonly LLMClientFactory _factory;
    private readonly string _preferredProvider;
    private readonly ILogger _logger;
    private string _systemPrompt;

    public string Name { get; }
    public string Role { get; }

    public Agent(
        string name,
        string role,
        string systemPrompt,
        LLMClientFactory factory,
        string preferredProvider,
        ILogger logger)
    {
        Name = name;
        Role = role;
        _systemPrompt = systemPrompt;
        _factory = factory;
        _preferredProvider = preferredProvider;
        _logger = logger;
    }

    public void SetSystemPrompt(string prompt) => _systemPrompt = prompt;

    public int GetMaxInputChars(string? modelOverride)
    {
        var target = !string.IsNullOrEmpty(modelOverride) ? modelOverride : _preferredProvider;
        return _factory.GetClient(target).MaxInputChars;
    }

    public async Task<string> ExecuteAsync(string input, string? modelOverride = null, CancellationToken ct = default)
    {
        _logger.LogInformation("[{Agent}] Executing...", Name);

        string targetProviderOrModel = !string.IsNullOrEmpty(modelOverride) ? modelOverride : _preferredProvider;
        var client = _factory.GetClient(targetProviderOrModel);

        string raw;

        try
        {
            raw = await client.CompleteAsync(_systemPrompt, input, modelOverride ?? "", ct);
        }
        catch (HttpRequestException ex) when (IsAuthError(ex))
        {
            // API key rejected at runtime → try another available provider
            _logger.LogWarning("[{Agent}] Provider '{Provider}' returned auth error ({Status}). Trying fallback...",
                Name, client.ProviderName, ex.StatusCode);

            raw = await FallbackCompleteAsync(client, input, modelOverride, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            // Payload too large for this provider (e.g. GitHub Models on large JSON).
            // Fall back to the next available provider rather than crashing.
            _logger.LogWarning(
                "[{Agent}] Provider '{Provider}' rejected payload (413 — too large, {Chars} chars). Trying fallback...",
                Name, client.ProviderName, input.Length);

            raw = await FallbackCompleteAsync(client, input, modelOverride, ct);
        }

        var json = ExtractJson(raw);
        if (json != null)
            return json;

        _logger.LogWarning("[{Agent}] Non-JSON response, retrying with same provider...", Name);
        ct.ThrowIfCancellationRequested();

        client = _factory.GetClient(targetProviderOrModel);
        raw = await client.CompleteAsync(_systemPrompt, input, modelOverride ?? "", ct);
        json = ExtractJson(raw);

        // Return raw content if JSON extraction still fails — supports non-JSON output types
        return json ?? raw;
    }
    private async Task<string> FallbackCompleteAsync(
        ILLMClient failedClient, string input, string? modelOverride, CancellationToken ct)
    {
        var fallback = _factory.GetFallbackClient(failedClient.ProviderName)
            ?? throw new InvalidOperationException(
                $"[{Name}] Provider '{failedClient.ProviderName}' failed and no fallback provider is available.");

        _logger.LogWarning("[{Agent}] Retrying with fallback provider '{Fallback}'.", Name, fallback.ProviderName);
        return await fallback.CompleteAsync(_systemPrompt, input, modelOverride ?? "", ct);
    }

    private static bool IsAuthError(HttpRequestException ex)
        => ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    internal static string? ExtractJson(string text)
    {
        text = text.Trim();

        if ((text.StartsWith('[') && text.EndsWith(']')) ||
            (text.StartsWith('{') && text.EndsWith('}')))
            return text;

        int startArray = text.IndexOf('[');
        int startObj = text.IndexOf('{');

        if (startArray < 0 && startObj < 0)
            return null;

        int start;
        char close;

        if (startArray >= 0 && (startObj < 0 || startArray < startObj))
        {
            start = startArray;
            close = ']';
        }
        else
        {
            start = startObj;
            close = '}';
        }

        int end = text.LastIndexOf(close);
        if (end <= start) return null;

        return text[start..(end + 1)];
    }
}
