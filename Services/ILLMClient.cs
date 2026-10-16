namespace AgentController.Services;

public interface ILLMClient
{
    string ProviderName { get; }

    /// <summary>True when the provider has a non-empty, plausible API key/token.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Maximum user message length (chars) this provider accepts in a single request.
    /// The Orchestrator uses this to decide whether to chunk the input before calling the agent.
    /// </summary>
    int MaxInputChars { get; }

    Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        string model,
        CancellationToken ct = default);
}