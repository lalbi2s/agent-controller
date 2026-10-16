namespace AgentController.Services;

public interface ILLMClientFactory
{
    /// <summary>
    /// Returns the requested provider when available, based on the provider name or the model name (e.g., "gpt-4o").
    /// Falls back to the first available provider and logs a warning when the preferred one is unavailable.
    /// Throws if no provider is available at all.
    /// </summary>
    ILLMClient GetClient(string preferredProviderOrModel);

    /// <summary>Returns the first available provider whose name differs from <paramref name="excludeProvider"/>. Null if none.</summary>
    ILLMClient? GetFallbackClient(string excludeProvider);
}