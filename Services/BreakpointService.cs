using System.Collections.Concurrent;

namespace AgentController.Services;

public interface IBreakpointService
{
    string CreateRun();
    Task WaitForResumeAsync(string runId, CancellationToken ct = default);
    void Resume(string runId);
    void CleanupRun(string runId);
}

public class BreakpointService : IBreakpointService
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _gates = new();

    public string CreateRun() => Guid.NewGuid().ToString("N")[..12];

    public async Task WaitForResumeAsync(string runId, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gates[runId] = tcs;

        await using var reg = ct.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        _gates.TryRemove(runId, out _);
    }

    public void Resume(string runId)
    {
        if (_gates.TryRemove(runId, out var tcs))
            tcs.TrySetResult();
    }

    public void CleanupRun(string runId)
    {
        if (_gates.TryRemove(runId, out var tcs))
            tcs.TrySetCanceled();
    }
}
