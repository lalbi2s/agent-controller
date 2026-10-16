using System.Diagnostics;
using System.Text.Json;
using AgentController.Agents;
using AgentController.Models;
using AgentController.Services;

namespace AgentController;

public class Orchestrator
{
    private readonly AgentFactory _agentFactory;
    private readonly PromptLoader _promptLoader;
    private readonly BreakpointService _breakpointService;
    private readonly FileChunkingService _chunking;
    private readonly ILogger<Orchestrator> _logger;

    public Orchestrator(
        AgentFactory agentFactory,
        PromptLoader promptLoader,
        BreakpointService breakpointService,
        FileChunkingService chunking,
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
        ValidateAgentTypes(pipeline);

        var totalTime = Stopwatch.StartNew();
        var agents = await PrepareAgentsAsync(pipeline, agentConfigs, emit);
        var state = new RunState(fileContent);

        // A while loop lets a failed review send the run back to an earlier agent
        int index = 0;
        while (index < agents.Count)
        {
            var current = agents[index];
            var info = current.Info;

            await CheckBreakpointAsync(current, pipeline.Name, emit, runId, breakpoints, ct);
            ct.ThrowIfCancellationRequested();
            await emit(MakeEvent(PipelineEventType.AgentStart, info.Name, info.Step, $"Running {info.Name}..."));

            var agentTime = Stopwatch.StartNew();
            if (info.Type == AgentTypes.Check)
            {
                await RunCheckAsync(current, state, emit);
            }
            else
            {
                var isFirstRun = index == 0 && state.Corrections is null;
                var input = BuildInput(isFirstRun, state, fileContent, testName);
                var output = await RunAgentAsync(current, input, isFirstRun, pipeline.OutputType, emit, ct);
                await ApplyOutputAsync(current, output, state, emit);
            }
            agentTime.Stop();
            state.Corrections = null;

            state.AgentStats[info.Role] = new { status = "ok", durationMs = agentTime.ElapsedMilliseconds };
            await emit(MakeEvent(PipelineEventType.AgentComplete, info.Name, info.Step,
                $"{info.Name} completed in {agentTime.ElapsedMilliseconds}ms"));

            var correctionTarget = await StartCorrectionAsync(agents, index, pipeline, state, emit);
            index = correctionTarget >= 0 ? correctionTarget : index + 1;
        }

        totalTime.Stop();
        await EmitCompletion(state, totalTime.ElapsedMilliseconds, emit);
    }

    private static void ValidateAgentTypes(PipelineInfo pipeline)
    {
        var unknown = pipeline.Agents.FirstOrDefault(a => !AgentTypes.IsValid(a.Type));
        if (unknown is not null)
            throw new InvalidOperationException(
                $"Agent '{unknown.Name}' has an unknown type '{unknown.Type}'. Use generate, review or check.");

        var unknownChecker = pipeline.Agents.FirstOrDefault(a => a.Type == AgentTypes.Check && !Checkers.Exists(a.Checker));
        if (unknownChecker is not null)
            throw new InvalidOperationException(
                $"Agent '{unknownChecker.Name}' uses an unknown checker '{unknownChecker.Checker}'.");
    }

    private async Task<List<PlannedAgent>> PrepareAgentsAsync(
        PipelineInfo pipeline, Dictionary<string, AgentConfig> agentConfigs, Func<PipelineEvent, Task> emit)
    {
        var agents = new List<PlannedAgent>();

        foreach (var info in pipeline.Agents.OrderBy(a => a.Step))
        {
            var config = agentConfigs.GetValueOrDefault(info.Role) ?? new AgentConfig { Role = info.Role };

            if (!config.Enabled)
            {
                await emit(MakeEvent(PipelineEventType.AgentSkip, info.Name, info.Step, $"Agent {info.Name} disabled"));
                continue;
            }

            // A check runs in code: it has no prompt and no LLM
            var agent = info.Type == AgentTypes.Check ? null : _agentFactory.Create(pipeline.Name, info, config);
            agents.Add(new PlannedAgent(agent, config, info));
        }

        return agents;
    }

    // The first agent reads the raw files. The next ones read the files and the current main output.
    private static string BuildInput(bool isFirstRun, RunState state, string fileContent, string testName)
    {
        if (isFirstRun)
            return fileContent;

        var input = $"--- FILES ---\n{fileContent}\n\n--- MAIN OUTPUT ---\n{state.MainOutput}";
        if (state.Corrections is not null)
            input += $"\n\n--- ERRORS TO FIX ---\n{state.Corrections}";

        return $"{input}\n\nSource name: {testName}";
    }

    private async Task<string> RunAgentAsync(
        PlannedAgent planned, string input, bool splitLargeInput, string outputType,
        Func<PipelineEvent, Task> emit, CancellationToken ct)
    {
        try
        {
            return await ExecuteWithChunking(planned, input, splitLargeInput, outputType, emit, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await emit(MakeEvent(PipelineEventType.AgentError, planned.Info.Name, planned.Info.Step, ex.Message));
            throw;
        }
    }

    // Only the raw files are split: the output of an earlier agent cannot be cut into independent parts
    private async Task<string> ExecuteWithChunking(
        PlannedAgent planned, string input, bool splitLargeInput, string outputType,
        Func<PipelineEvent, Task> emit, CancellationToken ct)
    {
        var agent = planned.Agent!;
        // A review agent always answers with JSON, whatever the output type of the pipeline
        bool expectJson = planned.Info.Type != AgentTypes.Generate || outputType == "json";
        int maxChars = agent.GetMaxInputChars();

        if (!splitLargeInput || !_chunking.NeedsChunking(input, maxChars))
            return await agent.ExecuteAsync(input, expectJson, ct);

        int chunkSize = (int)(maxChars * 0.8); // leave room for the system prompt
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
            results.Add(await agent.ExecuteAsync(chunks[i], expectJson, ct));
        }

        return _chunking.MergeResults(results, outputType);
    }

    // What an agent output means depends only on the agent type, never on what the model wrote
    private static async Task ApplyOutputAsync(
        PlannedAgent planned, string output, RunState state, Func<PipelineEvent, Task> emit)
    {
        var info = planned.Info;

        switch (info.Type)
        {
            case AgentTypes.Generate:
                state.MainOutput = output;
                await ReadMetricsAsync(output, info.Name, info.Step, state, emit);
                break;

            case AgentTypes.Review:
                await ReadReviewAsync(output, info.Name, info.Step, state, emit);
                break;
        }
    }

    // A check gives the same kind of result as a review, so the correction loop treats both the same way
    private static async Task RunCheckAsync(PlannedAgent planned, RunState state, Func<PipelineEvent, Task> emit)
    {
        var info = planned.Info;
        var problems = Checkers.Run(info.Checker!, state.MainOutput);

        state.LastReview = new ReviewResult
        {
            Issues = problems.Select(p => new ReviewIssue { Severity = "error", Description = p }).ToList()
        };

        await emit(new PipelineEvent
        {
            Type = PipelineEventType.Review,
            Agent = info.Name,
            Step = info.Step,
            Data = state.LastReview,
            Message = problems.Count == 0 ? "No problem found" : $"{problems.Count} problems found"
        });
    }

    // Shows the metrics list in the interface when a generate agent returns one. It never changes the routing.
    private static async Task ReadMetricsAsync(
        string output, string agentName, int step, RunState state, Func<PipelineEvent, Task> emit)
    {
        try
        {
            var result = JsonSerializer.Deserialize<ExtractorResult>(output);
            if (result is null || result.Metrics.Count == 0)
                return;

            state.Metrics = result.Metrics;
        }
        catch (JsonException)
        {
            return;
        }

        await emit(new PipelineEvent
        {
            Type = PipelineEventType.Metrics,
            Agent = agentName,
            Step = step,
            Data = new { count = state.Metrics.Count, metrics = state.Metrics },
            Message = $"{state.Metrics.Count} metrics found"
        });
    }

    private static async Task ReadReviewAsync(
        string output, string agentName, int step, RunState state, Func<PipelineEvent, Task> emit)
    {
        state.LastReview = null;

        try
        {
            state.LastReview = JsonSerializer.Deserialize<ReviewResult>(output);
        }
        catch (JsonException) { }

        if (state.LastReview is null)
        {
            await emit(MakeEvent(PipelineEventType.AgentLog, agentName, step,
                "The review could not be read, no correction was started"));
            return;
        }

        await emit(new PipelineEvent
        {
            Type = PipelineEventType.Review,
            Agent = agentName,
            Step = step,
            Data = state.LastReview,
            Message = $"{state.LastReview.Issues.Count} issues, {state.LastReview.Suggestions.Count} suggestions"
        });
    }

    // After a review with errors, goes back to the closest generate agent before it. Returns -1 when no correction starts.
    private static async Task<int> StartCorrectionAsync(
        List<PlannedAgent> agents, int index, PipelineInfo pipeline, RunState state, Func<PipelineEvent, Task> emit)
    {
        var info = agents[index].Info;
        if (info.Type is not (AgentTypes.Review or AgentTypes.Check) || state.LastReview is not { HasErrors: true } review)
            return -1;

        int target = index > 0 ? agents.FindLastIndex(index - 1, a => a.Info.Type == AgentTypes.Generate) : -1;
        if (target < 0 || state.CorrectionLoops >= pipeline.MaxCorrectionLoops)
        {
            // The run goes on, but the errors of a check should not pass unnoticed
            if (info.Type == AgentTypes.Check)
                await emit(MakeEvent(PipelineEventType.AgentLog, info.Name, info.Step,
                    $"{review.Errors.Count()} problems remain after {state.CorrectionLoops} correction(s)"));
            return -1;
        }

        state.CorrectionLoops++;
        state.Corrections = string.Join("\n", review.Errors.Select(e => $"- {e.Description}"));
        state.AgentStats[info.Role] = new { status = "corrected", loops = state.CorrectionLoops };

        await emit(new PipelineEvent
        {
            Type = PipelineEventType.CorrectionLoop,
            Agent = info.Name,
            Step = info.Step,
            Message = $"Correction loop {state.CorrectionLoops}/{pipeline.MaxCorrectionLoops}",
            Data = new { iteration = state.CorrectionLoops, max = pipeline.MaxCorrectionLoops }
        });

        return target;
    }

    private async Task CheckBreakpointAsync(
        PlannedAgent planned, string pipelineName, Func<PipelineEvent, Task> emit,
        string? runId, HashSet<string>? breakpoints, CancellationToken ct)
    {
        var info = planned.Info;
        if (breakpoints is null || runId is null || !breakpoints.Contains(info.Role))
            return;

        _logger.LogInformation("Breakpoint before agent {Agent}", info.Name);

        // Register the pause before telling the interface, so a fast "Continue" cannot be missed
        var resumed = _breakpointService.WaitForResumeAsync(runId, ct);
        await emit(new PipelineEvent
        {
            Type = PipelineEventType.Breakpoint,
            Agent = info.Name,
            Step = info.Step,
            Message = $"Breakpoint before {info.Name}, waiting for resume",
            Data = new { runId, role = info.Role }
        });
        await resumed;

        // The prompt may have been edited while the run was paused
        if (planned.Agent is null)
            return;

        try
        {
            planned.Agent.SetSystemPrompt(_promptLoader.LoadPrompt(pipelineName, info.PromptFile));
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning("Prompt {File} not found after resume, keeping the previous one", info.PromptFile);
        }
    }

    private static Task EmitCompletion(RunState state, long totalMs, Func<PipelineEvent, Task> emit) =>
        emit(new PipelineEvent
        {
            Type = PipelineEventType.Complete,
            Message = "Pipeline completed",
            Data = new
            {
                output = state.MainOutput,
                stats = new
                {
                    totalDurationMs = totalMs,
                    metricsCount = state.Metrics.Count,
                    outputSize = state.MainOutput.Length,
                    correctionLoops = state.CorrectionLoops,
                    agents = state.AgentStats
                }
            }
        });

    private static PipelineEvent MakeEvent(PipelineEventType type, string agent, int step, string message) =>
        new() { Type = type, Agent = agent, Step = step, Message = message };

    private record PlannedAgent(Agent? Agent, AgentConfig Config, AgentInfo Info);

    private class RunState(string mainOutput)
    {
        public string MainOutput { get; set; } = mainOutput;
        public List<Metric> Metrics { get; set; } = [];
        public ReviewResult? LastReview { get; set; }
        public string? Corrections { get; set; }
        public int CorrectionLoops { get; set; }
        public Dictionary<string, object> AgentStats { get; } = [];
    }
}
