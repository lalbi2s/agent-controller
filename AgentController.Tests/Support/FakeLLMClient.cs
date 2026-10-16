using AgentController.Services;

namespace AgentController.Tests.Support;

/// <summary>Replaces a real provider. Answers come from a queue, nothing goes to the network.</summary>
public class FakeLLMClient : ILLMClient
{
    private readonly Queue<object> _answers = new();

    public FakeLLMClient(string providerName = "anthropic", bool isAvailable = true, int maxInputChars = 100_000)
    {
        ProviderName = providerName;
        IsAvailable = isAvailable;
        MaxInputChars = maxInputChars;
    }

    public string ProviderName { get; }
    public bool IsAvailable { get; set; }
    public int MaxInputChars { get; }
    public Exception? TestConnectionError { get; set; }

    public List<LlmCall> Calls { get; } = [];

    /// <summary>Queues a text answer, or an exception to throw.</summary>
    public FakeLLMClient Reply(params object[] answers)
    {
        foreach (var answer in answers)
            _answers.Enqueue(answer);
        return this;
    }

    public Task<string> CompleteAsync(string systemPrompt, string userMessage, string model, CancellationToken ct = default)
    {
        Calls.Add(new LlmCall(systemPrompt, userMessage, model));

        if (_answers.Count == 0)
            throw new InvalidOperationException($"No answer queued for call {Calls.Count} of {ProviderName}");

        return _answers.Dequeue() switch
        {
            Exception ex => Task.FromException<string>(ex),
            var text => Task.FromResult((string)text)
        };
    }

    public Task TestConnectionAsync(CancellationToken ct = default) =>
        TestConnectionError is null ? Task.CompletedTask : Task.FromException(TestConnectionError);
}

public record LlmCall(string SystemPrompt, string UserMessage, string Model);
