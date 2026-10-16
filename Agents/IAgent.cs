namespace AgentController.Agents;

public interface IAgent
{
    string Name { get; }
    string Role { get; }
    Task<string> ExecuteAsync(string input, string? modelOverride = null, CancellationToken ct = default);
    void SetSystemPrompt(string prompt);

    /// <summary>
    /// Returns the MaxInputChars of the provider that will handle the given model override.
    /// Used by the Orchestrator to decide the chunk size before calling ExecuteAsync.
    /// </summary>
    int GetMaxInputChars(string? modelOverride);
}
