using AgentController.Agents;
using AgentController.Models;
using AgentController.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentController.Tests.Support;

/// <summary>A pipelines folder in a temporary directory, with the real services wired on fake providers.</summary>
public sealed class TestWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "agent-controller-tests", Guid.NewGuid().ToString("N"));
    public PromptLoader PromptLoader { get; }

    public TestWorkspace()
    {
        Directory.CreateDirectory(Root);
        PromptLoader = new PromptLoader(Path.Combine(Root, "pipelines"), NullLogger<PromptLoader>.Instance);
    }

    public Orchestrator CreateOrchestrator(params ILLMClient[] clients)
    {
        var factory = new LLMClientFactory(clients, NullLogger<LLMClientFactory>.Instance);
        var config = new ConfigurationBuilder().Build();
        var agentFactory = new AgentFactory(factory, PromptLoader, NullLoggerFactory.Instance, config);

        return new Orchestrator(
            agentFactory, PromptLoader, new BreakpointService(), new FileChunkingService(),
            NullLogger<Orchestrator>.Instance);
    }

    /// <summary>Creates a pipeline named "test". Each agent is (role, type), its prompt is "prompt of {role}".</summary>
    public PipelineInfo CreatePipeline(string outputType, int maxCorrectionLoops, params (string Role, string Type)[] agents)
    {
        var pipeline = new PipelineInfo
        {
            Name = "test",
            OutputType = outputType,
            MaxCorrectionLoops = maxCorrectionLoops,
            Agents = agents.Select((a, i) => new AgentInfo
            {
                Role = a.Role,
                Name = a.Role,
                Step = i + 1,
                PromptFile = $"{a.Role}.md",
                Type = a.Type
            }).ToList()
        };
        var prompts = agents.ToDictionary(a => $"{a.Role}.md", a => $"prompt of {a.Role}");

        return PromptLoader.CreatePipeline(pipeline, prompts);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }
}

public static class Run
{
    /// <summary>Runs a pipeline and returns the events in order.</summary>
    public static async Task<List<PipelineEvent>> PipelineAsync(
        Orchestrator orchestrator, PipelineInfo pipeline, Dictionary<string, AgentConfig>? configs = null)
    {
        var events = new List<PipelineEvent>();
        await orchestrator.RunPipelineAsync(
            "file content", "my-test", pipeline, configs ?? [], evt => { events.Add(evt); return Task.CompletedTask; });
        return events;
    }
}
