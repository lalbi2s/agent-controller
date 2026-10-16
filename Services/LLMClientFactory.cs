namespace AgentController.Services;

public class LLMClientFactory
{
    private readonly IReadOnlyList<ILLMClient> _clients;
    private readonly ILogger<LLMClientFactory> _logger;

    public LLMClientFactory(IEnumerable<ILLMClient> clients, ILogger<LLMClientFactory> logger)
    {
        _clients = clients.ToList();
        _logger = logger;
    }

    public ILLMClient? Find(string provider) =>
        _clients.FirstOrDefault(c => c.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns the requested provider when it has a key, otherwise the first provider that has one.
    /// Throws when no provider is available.
    /// </summary>
    public ILLMClient GetClient(string provider)
    {
        var preferred = Find(provider);
        if (preferred is { IsAvailable: true })
            return preferred;

        var fallback = _clients.FirstOrDefault(c => c.IsAvailable)
            ?? throw new InvalidOperationException(
                "No LLM provider is available. Add an API key on the API keys page or in the configuration.");

        _logger.LogWarning("Provider {Preferred} has no API key, using {Fallback} instead", provider, fallback.ProviderName);
        return fallback;
    }

    /// <summary>Returns the first available provider other than <paramref name="excludeProvider"/>, or null.</summary>
    public ILLMClient? GetFallbackClient(string excludeProvider) =>
        _clients.FirstOrDefault(c => c.IsAvailable && !c.ProviderName.Equals(excludeProvider, StringComparison.OrdinalIgnoreCase));
}
