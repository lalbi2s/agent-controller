using System.Diagnostics;
using System.Text.Json;
using AgentController.Agents;
using AgentController.Models;
using AgentController.Services;

namespace AgentController;

public interface IOrchestrator
{
    Task RunPipelineAsync(
        string fileContent,
        string testName,
        PipelineInfo pipeline,
        Dictionary<string, AgentConfig> agentConfigs,
        Func<PipelineEvent, Task> emit,
        CancellationToken ct = default,
        string? runId = null,
        HashSet<string>? breakpoints = null);
}

public class Orchestrator : IOrchestrator
{
    private readonly IAgentFactory _agentFactory;
    private readonly IPromptLoader _promptLoader;
    private readonly IBreakpointService _breakpointService;
    private readonly IFileChunkingService _chunking;
    private readonly ILogger<Orchestrator> _logger;

    public Orchestrator(
        IAgentFactory agentFactory,
        IPromptLoader promptLoader,
        IBreakpointService breakpointService,
        IFileChunkingService chunking,
        ILogger<Orchestrator> logger)
    {
        _agentFactory = agentFactory;
        _promptLoader = promptLoader;
        _breakpointService = breakpointService;
        _chunking = chunking;
        _logger = logger;
    }

    public async Task RunPipelineAsync(
        string fileContent,
        string testName,
        PipelineInfo pipeline,
        Dictionary<string, AgentConfig> agentConfigs,
        Func<PipelineEvent, Task> emit,
        CancellationToken ct = default,
        string? runId = null,
        HashSet<string>? breakpoints = null)
    {
        var sw = Stopwatch.StartNew();
        var enabledAgents = BuildAgentList(pipeline, agentConfigs, emit);
        await SkipDisabledAgents(pipeline, agentConfigs, emit);

        string mainOutput = fileContent;
        string previousOutput = fileContent;
        var agentStats = new Dictionary<string, object>();
        List<Metric> metrics = [];
        TestProfile testProfile = new();
        ReviewResult? lastReview = null;
        int correctionLoops = 0;

        int currentAgentIndex = 0;

        while (currentAgentIndex < enabledAgents.Count)
        {
            var (agent, config, info) = enabledAgents[currentAgentIndex];
            var role = info.Role;
            var step = info.Step;

            var effectiveInputSource = string.IsNullOrEmpty(info.InputSource)
                ? (currentAgentIndex == 0 ? "files" : "previous")
                : info.InputSource.ToLower();

            await CheckBreakpointAsync(role, agent, step, pipeline.Name, config.PromptFile ?? info.PromptFile, emit, runId, breakpoints, ct);
            await emit(MakeEvent(PipelineEventType.AgentStart, agent.Name, step, $"Running {agent.Name}..."));
            ct.ThrowIfCancellationRequested();

            var agentSw = Stopwatch.StartNew();

            // 1. Input selon effectiveInputSource
            string rawInput = effectiveInputSource switch
            {
                "files"       => fileContent,
                "main-output" => string.IsNullOrEmpty(mainOutput) ? fileContent : mainOutput,
                "all"         => $"--- FILES ---\n{fileContent}\n\n--- MAIN OUTPUT ---\n{mainOutput}",
                _             => string.IsNullOrEmpty(previousOutput) ? fileContent : previousOutput
            };
            var input = effectiveInputSource == "files"
                ? rawInput
                : BuildAgentInput(rawInput, testName);

            var output = await ExecuteWithChunking(agent, input, config.Model, pipeline.OutputType, emit, ct);
            agentSw.Stop();

            var behavior = string.IsNullOrEmpty(info.OutputBehavior)
                ? DetectBehavior(output)
                : info.OutputBehavior.ToLower();

            ProcessAgentOutput(behavior, output, ref metrics, ref testProfile, ref lastReview, emit, step, agent.Name);

            agentStats[role] = new { status = "ok", durationMs = agentSw.ElapsedMilliseconds };
            await emit(MakeEvent(PipelineEventType.AgentComplete, agent.Name, step,
                $"{agent.Name} completed in {agentSw.ElapsedMilliseconds}ms"));

            // 4. Routage
            bool jumped = false;
            switch (behavior)
            {
                case "propagate":
                    mainOutput = output;
                    previousOutput = output;
                    break;

                case "review":
                    if (lastReview?.HasErrors == true && correctionLoops < pipeline.MaxCorrectionLoops)
                    {
                        var targetIndex = !string.IsNullOrEmpty(info.CorrectionTarget)
                            ? enabledAgents.FindIndex(a => a.Info.Role == info.CorrectionTarget)
                            : currentAgentIndex - 1;

                        if (targetIndex >= 0)
                        {
                            correctionLoops++;
                            var corrections = string.Join("\n", lastReview.Issues
                                .Where(i => i.Severity.Equals("error", StringComparison.OrdinalIgnoreCase))
                                .Select(i => $"- {i.Description}"));

                            await emit(new PipelineEvent
                            {
                                Type = PipelineEventType.CorrectionLoop,
                                Agent = agent.Name,
                                Step = step,
                                Message = $"Correction loop {correctionLoops}/{pipeline.MaxCorrectionLoops}",
                                Data = new { iteration = correctionLoops, max = pipeline.MaxCorrectionLoops }
                            });

                            previousOutput = $"Current output:\n{mainOutput}\n\nErrors to fix:\n{corrections}";
                            currentAgentIndex = targetIndex;
                            jumped = true;
                            agentStats[role] = new { status = "corrected", loops = correctionLoops };
                        }
                        else { previousOutput = output; }
                    }
                    else { previousOutput = output; }
                    break;

                case "validate":
                    previousOutput = output;
                    break;

                case "enrich":
                    mainOutput = $"{mainOutput}\n\n{output}";
                    previousOutput = output;
                    break;

                default: // "discard" ou inconnu
                    previousOutput = output;
                    break;
            }

            if (!jumped)
                currentAgentIndex++;
        }

        sw.Stop();
        await EmitCompletion(mainOutput, metrics, testProfile, agentStats, correctionLoops, sw.ElapsedMilliseconds, emit);
    }

    private List<(IAgent Agent, AgentConfig Config, AgentInfo Info)> BuildAgentList(
        PipelineInfo pipeline, Dictionary<string, AgentConfig> agentConfigs, Func<PipelineEvent, Task> emit)
    {
        var result = new List<(IAgent, AgentConfig, AgentInfo)>();

        foreach (var agentInfo in pipeline.Agents.OrderBy(a => a.Step))
        {
            var config = agentConfigs.GetValueOrDefault(agentInfo.Role)
                ?? new AgentConfig { Role = agentInfo.Role, Enabled = true };

            if (!config.Enabled) continue;

            var agent = _agentFactory.Create(pipeline.Name, agentInfo, config.PromptFile);
            result.Add((agent, config, agentInfo));
        }

        return result;
    }

    private static async Task SkipDisabledAgents(
        PipelineInfo pipeline, Dictionary<string, AgentConfig> agentConfigs, Func<PipelineEvent, Task> emit)
    {
        foreach (var agentInfo in pipeline.Agents.OrderBy(a => a.Step))
        {
            var config = agentConfigs.GetValueOrDefault(agentInfo.Role);
            if (config is { Enabled: false })
            {
                await emit(new PipelineEvent
                {
                    Type = PipelineEventType.AgentSkip,
                    Agent = agentInfo.Name,
                    Step = agentInfo.Step,
                    Message = $"Agent {agentInfo.Name} disabled"
                });
            }
        }
    }

    private async Task<string> ExecuteWithChunking(
        IAgent agent, string input, string model, string outputType,
        Func<PipelineEvent, Task> emit, CancellationToken ct)
    {
        int maxChars = agent.GetMaxInputChars(model);
        int chunkSize = (int)(maxChars * 0.8); // leave room for the system prompt

        if (!_chunking.NeedsChunking(input, maxChars))
            return await agent.ExecuteAsync(input, model, ct);

        var chunks = _chunking.Split(input, chunkSize);
        var results = new List<string>();

        for (int i = 0; i < chunks.Count; i++)
        {
            await emit(new PipelineEvent
            {
                Type = PipelineEventType.ChunkProgress,
                Agent = agent.Name,
                Message = $"Processing chunk {i + 1}/{chunks.Count}",
                Data = new { current = i + 1, total = chunks.Count }
            });

            ct.ThrowIfCancellationRequested();
            results.Add(await agent.ExecuteAsync(chunks[i], model, ct));
        }

        return _chunking.MergeResults(results, outputType);
    }

 private static string BuildAgentInput(string previousOutput, string sourceName)
{
    return $"Input data:\n{previousOutput}\n\nSource name: {sourceName}";
}
    private void ProcessAgentOutput(
    string behavior, string output,
    ref List<Metric> metrics, ref TestProfile testProfile,
    ref ReviewResult? lastReview,
    Func<PipelineEvent, Task> emit, int step, string agentName)
{
    if (behavior.Equals("review", StringComparison.OrdinalIgnoreCase))
    {
        TryParseReviewOutput(output, ref lastReview, emit, step, agentName);
        return;
    }

    // Only try metric parsing if output looks like JSON
    if (output.TrimStart().StartsWith('{') || output.TrimStart().StartsWith('['))
        TryParseExtractorOutput(output, ref metrics, ref testProfile, emit, step, agentName);
}

    private static void TryParseExtractorOutput(
        string output, ref List<Metric> metrics, ref TestProfile testProfile,
        Func<PipelineEvent, Task> emit, int step, string agentName)
    {
        try
        {
            var result = JsonSerializer.Deserialize<ExtractorResult>(output);
            if (result?.Metrics is not null)
            {
                metrics = result.Metrics;
                testProfile = result.TestProfile ?? new TestProfile();
            }
            else
            {
                metrics = JsonSerializer.Deserialize<List<Metric>>(output) ?? [];
            }
        }
        catch
        {
            try { metrics = JsonSerializer.Deserialize<List<Metric>>(output) ?? []; }
            catch { /* not metric-shaped output — that's fine for generic pipelines */ }
        }

        if (metrics.Count > 0)
        {
            emit(new PipelineEvent
            {
                Type = PipelineEventType.Metrics,
                Agent = agentName,
                Step = step,
                Data = new { count = metrics.Count, metrics, testProfile },
                Message = $"{metrics.Count} metrics found"
            }).GetAwaiter().GetResult();
        }
    }

    private static void TryParseReviewOutput(
        string output, ref ReviewResult? lastReview,
        Func<PipelineEvent, Task> emit, int step, string agentName)
    {
        try
        {
            lastReview = JsonSerializer.Deserialize<ReviewResult>(output) ?? new ReviewResult();
            emit(new PipelineEvent
            {
                Type = PipelineEventType.Review,
                Agent = agentName,
                Step = step,
                Data = lastReview,
                Message = $"{lastReview.Issues.Count} issues, {lastReview.Suggestions.Count} suggestions"
            }).GetAwaiter().GetResult();
        }
        catch { /* not review-shaped output — skip */ }
    }

    private async Task CheckBreakpointAsync(
        string role, IAgent agent, int step,
        string pipelineName, string promptFile,
        Func<PipelineEvent, Task> emit, string? runId,
        HashSet<string>? breakpoints, CancellationToken ct)
    {
        if (breakpoints == null || runId == null || !breakpoints.Contains(role)) return;

        _logger.LogInformation("Breakpoint hit before agent {Agent}", agent.Name);
        await emit(new PipelineEvent
        {
            Type = PipelineEventType.Breakpoint,
            Agent = agent.Name,
            Step = step,
            Message = $"Breakpoint before {agent.Name} — waiting for resume",
            Data = new { runId, role }
        });

        await _breakpointService.WaitForResumeAsync(runId, ct);

        try
        {
            var freshPrompt = _promptLoader.LoadPrompt(pipelineName, promptFile);
            agent.SetSystemPrompt(freshPrompt);
        }
        catch { /* keep current prompt if reload fails */ }

        _logger.LogInformation("Resumed after breakpoint on {Agent}", agent.Name);
    }

  private static async Task EmitCompletion(
    string output, List<Metric> metrics, TestProfile testProfile,
    Dictionary<string, object> agentStats, int correctionLoops,
    long totalMs, Func<PipelineEvent, Task> emit)
{
    await emit(new PipelineEvent
    {
        Type = PipelineEventType.Complete,
        Message = "Pipeline completed",
        Data = new
        {
            output,
            stats = new
            {
                totalDurationMs = totalMs,
                metricsCount = metrics.Count,
                testType = testProfile.Type,
                maxVUs = testProfile.MaxVUs,
                outputSize = output.Length,
                correctionLoops,
                agents = agentStats
            }
        }
    });
}

    private static int CountPanels(string output)
    {
        try
        {
            var doc = JsonDocument.Parse(output);
            if (doc.RootElement.TryGetProperty("panels", out var panels))
                return panels.GetArrayLength();
        }
        catch { /* not JSON or no panels — fine */ }
        return 0;
    }

    private static string DetectBehavior(string output)
    {
        if (!output.TrimStart().StartsWith('{'))
            return "propagate";

        try
        {
            var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            if (root.TryGetProperty("issues", out _))
                return "review";

            if (root.TryGetProperty("valid", out _))
                return "validate";
        }
        catch { /* not valid JSON — treat as propagate */ }

        return "propagate";
    }

    private static PipelineEvent MakeEvent(PipelineEventType type, string agent, int step, string message) =>
        new() { Type = type, Agent = agent, Step = step, Message = message };
}