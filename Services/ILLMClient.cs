namespace AgentController.Services;

public interface ILLMClient
{
    string ProviderName { get; }

    /// <summary>True when an API key is configured for this provider.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Maximum user message length (chars) accepted in a single request.
    /// The orchestrator splits larger files before calling the agent.
    /// </summary>
    int MaxInputChars { get; }

    /// <summary>Sends a request. An empty model means the default model of the provider.</summary>
    Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        string model,
        CancellationToken ct = default);

    /// <summary>Makes a cheap call to check the key. Throws HttpRequestException when it fails.</summary>
    Task TestConnectionAsync(CancellationToken ct = default);
}
