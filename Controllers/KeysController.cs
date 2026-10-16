using System.Text.Json.Serialization;
using AgentController.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentController.Controllers;

/// <summary>
/// Manages the provider API keys. A key is only ever received: the API answers with its status and last 4 characters.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class KeysController : ControllerBase
{
    private const int MaxKeyLength = 500;
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(15);

    private readonly ApiKeyStore _store;
    private readonly ApiKeyResolver _resolver;
    private readonly LLMClientFactory _clients;

    public KeysController(ApiKeyStore store, ApiKeyResolver resolver, LLMClientFactory clients)
    {
        _store = store;
        _resolver = resolver;
        _clients = clients;
    }

    [HttpGet]
    public IActionResult GetAll() =>
        Ok(ApiKeyResolver.Providers.Select(Describe));

    [HttpPut("{provider}")]
    public IActionResult Save(string provider, [FromBody] SaveKeyRequest body)
    {
        if (!ApiKeyResolver.IsKnownProvider(provider))
            return NotFound(new { error = "Unknown provider" });

        var key = body.Key?.Trim() ?? "";
        if (key.Length == 0 || key.Length > MaxKeyLength)
            return BadRequest(new { error = "The key is empty or too long" });

        _store.Set(provider.ToLowerInvariant(), key);
        return NoContent();
    }

    [HttpDelete("{provider}")]
    public IActionResult Delete(string provider)
    {
        if (!ApiKeyResolver.IsKnownProvider(provider))
            return NotFound(new { error = "Unknown provider" });

        _store.Delete(provider.ToLowerInvariant());
        return NoContent();
    }

    // The messages are fixed texts: they never include the key or the answer of the provider
    [HttpPost("{provider}/test")]
    public async Task<IActionResult> Test(string provider, CancellationToken ct)
    {
        var client = ApiKeyResolver.IsKnownProvider(provider) ? _clients.Find(provider) : null;
        if (client is null)
            return NotFound(new { error = "Unknown provider" });

        if (!client.IsAvailable)
            return Ok(new TestResult(false, "No key configured"));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TestTimeout);

        try
        {
            await client.TestConnectionAsync(timeout.Token);
            return Ok(new TestResult(true, "OK"));
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            return Ok(new TestResult(false, "Key rejected by provider"));
        }
        catch (HttpRequestException ex) when (ex.StatusCode is not null)
        {
            return Ok(new TestResult(false, $"Provider returned HTTP {(int)ex.StatusCode}"));
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return Ok(new TestResult(false, "Provider unreachable"));
        }
    }

    private KeyStatus Describe(string provider)
    {
        var (key, source) = _resolver.GetKeyAndSource(provider);
        var last4 = key is { Length: >= 8 } ? key[^4..] : "";
        return new KeyStatus(provider.ToLowerInvariant(), key is not null, source, last4);
    }
}

public record KeyStatus(string Provider, bool Configured, string Source, string Last4);

public record TestResult(bool Ok, string Message);

public class SaveKeyRequest
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }
}
