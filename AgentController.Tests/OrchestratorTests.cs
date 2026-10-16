using AgentController.Models;
using AgentController.Tests.Support;

namespace AgentController.Tests;

public class OrchestratorTests : IDisposable
{
    private const string ErrorReview = """{"issues":[{"severity":"error","description":"fix x"}],"suggestions":[]}""";
    private const string CleanReview = """{"issues":[],"suggestions":[]}""";

    private readonly TestWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    private static string FinalOutput(List<PipelineEvent> events)
    {
        var complete = events.Single(e => e.Type == PipelineEventType.Complete);
        return (string)complete.Data!.GetType().GetProperty("output")!.GetValue(complete.Data)!;
    }

    [Fact]
    public async Task Generate_agents_pass_their_output_to_the_next_agent()
    {
        var llm = new FakeLLMClient().Reply("""{"step":1}""", """{"step":2}""");
        var pipeline = _workspace.CreatePipeline("json", 2, ("first", "generate"), ("second", "generate"));

        var events = await Run.PipelineAsync(_workspace.CreateOrchestrator(llm), pipeline);

        Assert.Equal("""{"step":2}""", FinalOutput(events));
        Assert.Equal("file content", llm.Calls[0].UserMessage);
        Assert.Contains("""{"step":1}""", llm.Calls[1].UserMessage);
        Assert.Contains("file content", llm.Calls[1].UserMessage);
        Assert.Contains("my-test", llm.Calls[1].UserMessage);
    }

    [Fact]
    public async Task Review_with_errors_sends_the_run_back_to_the_last_generate_agent()
    {
        var llm = new FakeLLMClient().Reply("first result", "v1", ErrorReview, "v2", CleanReview);
        var pipeline = _workspace.CreatePipeline("text", 2, ("a", "generate"), ("b", "generate"), ("r", "review"));

        var events = await Run.PipelineAsync(_workspace.CreateOrchestrator(llm), pipeline);

        Assert.Equal(5, llm.Calls.Count);
        Assert.Equal("prompt of b", llm.Calls[3].SystemPrompt);
        Assert.Contains("--- ERRORS TO FIX ---\n- fix x", llm.Calls[3].UserMessage);
        Assert.Contains("v1", llm.Calls[3].UserMessage);
        Assert.Single(events, e => e.Type == PipelineEventType.CorrectionLoop);
        Assert.Equal("v2", FinalOutput(events));
    }

    [Fact]
    public async Task Correction_loops_stop_at_the_maximum()
    {
        var llm = new FakeLLMClient().Reply("v1", ErrorReview, "v2", ErrorReview);
        var pipeline = _workspace.CreatePipeline("text", 1, ("a", "generate"), ("r", "review"));

        var events = await Run.PipelineAsync(_workspace.CreateOrchestrator(llm), pipeline);

        Assert.Equal(4, llm.Calls.Count);
        Assert.Single(events, e => e.Type == PipelineEventType.CorrectionLoop);
        Assert.Equal("v2", FinalOutput(events));
    }

    [Fact]
    public async Task An_unreadable_review_does_not_start_a_correction()
    {
        var llm = new FakeLLMClient().Reply("v1", "no json here", "still no json");
        var pipeline = _workspace.CreatePipeline("text", 2, ("a", "generate"), ("r", "review"));

        var events = await Run.PipelineAsync(_workspace.CreateOrchestrator(llm), pipeline);

        Assert.DoesNotContain(events, e => e.Type == PipelineEventType.CorrectionLoop);
        Assert.Contains(events, e => e.Type == PipelineEventType.AgentLog);
        Assert.Equal("v1", FinalOutput(events));
    }

    [Fact]
    public async Task A_stale_review_is_not_used_after_a_later_unreadable_one()
    {
        var llm = new FakeLLMClient().Reply("v1", ErrorReview, "v2", "garbage", "garbage");
        var pipeline = _workspace.CreatePipeline("text", 5, ("a", "generate"), ("r", "review"));

        var events = await Run.PipelineAsync(_workspace.CreateOrchestrator(llm), pipeline);

        Assert.Single(events, e => e.Type == PipelineEventType.CorrectionLoop);
        Assert.Equal("v2", FinalOutput(events));
    }

    [Fact]
    public async Task The_agent_type_decides_the_behavior_not_the_shape_of_the_answer()
    {
        // A generate agent that happens to answer with an "issues" object is not a review
        var llm = new FakeLLMClient().Reply(ErrorReview);
        var pipeline = _workspace.CreatePipeline("json", 2, ("a", "generate"));

        var events = await Run.PipelineAsync(_workspace.CreateOrchestrator(llm), pipeline);

        Assert.DoesNotContain(events, e => e.Type is PipelineEventType.Review or PipelineEventType.CorrectionLoop);
        Assert.Equal(ErrorReview, FinalOutput(events));
    }

    [Fact]
    public async Task Review_and_validate_agents_do_not_replace_the_main_output()
    {
        var llm = new FakeLLMClient().Reply("""{"dashboard":true}""", CleanReview, """{"valid":true,"errors":[]}""");
        var pipeline = _workspace.CreatePipeline("json", 2, ("a", "generate"), ("r", "review"), ("v", "validate"));

        var events = await Run.PipelineAsync(_workspace.CreateOrchestrator(llm), pipeline);

        Assert.Contains("""{"dashboard":true}""", llm.Calls[2].UserMessage);
        Assert.DoesNotContain("issues", llm.Calls[2].UserMessage);
        Assert.Equal("""{"dashboard":true}""", FinalOutput(events));
    }

    [Fact]
    public async Task Disabled_agents_are_skipped()
    {
        var llm = new FakeLLMClient().Reply("only");
        var pipeline = _workspace.CreatePipeline("text", 2, ("a", "generate"), ("b", "generate"));
        var configs = new Dictionary<string, AgentConfig> { ["b"] = new() { Role = "b", Enabled = false } };

        var events = await Run.PipelineAsync(_workspace.CreateOrchestrator(llm), pipeline, configs);

        Assert.Single(llm.Calls);
        Assert.Single(events, e => e.Type == PipelineEventType.AgentSkip);
    }

    [Fact]
    public async Task Metrics_found_by_the_first_agent_survive_the_next_agents()
    {
        var metrics = """{"metrics":[{"name":"m1","type":"Trend"}]}""";
        var llm = new FakeLLMClient().Reply(metrics, """{"title":"dashboard"}""");
        var pipeline = _workspace.CreatePipeline("json", 2, ("a", "generate"), ("b", "generate"));

        var events = await Run.PipelineAsync(_workspace.CreateOrchestrator(llm), pipeline);

        Assert.Single(events, e => e.Type == PipelineEventType.Metrics);
        var complete = events.Single(e => e.Type == PipelineEventType.Complete);
        var stats = complete.Data!.GetType().GetProperty("stats")!.GetValue(complete.Data)!;
        Assert.Equal(1, stats.GetType().GetProperty("metricsCount")!.GetValue(stats));
    }

    [Fact]
    public async Task A_provider_failure_emits_an_agent_error_and_stops_the_run()
    {
        var llm = new FakeLLMClient().Reply(new HttpRequestException("Anthropic returned HTTP 500", null, System.Net.HttpStatusCode.InternalServerError));
        var pipeline = _workspace.CreatePipeline("text", 2, ("a", "generate"));
        var events = new List<PipelineEvent>();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            _workspace.CreateOrchestrator(llm).RunPipelineAsync(
                "file content", "t", pipeline, [], evt => { events.Add(evt); return Task.CompletedTask; }));

        Assert.Single(events, e => e.Type == PipelineEventType.AgentError);
    }

    [Fact]
    public async Task An_unknown_agent_type_is_refused()
    {
        var pipeline = _workspace.CreatePipeline("text", 2, ("a", "generate"));
        pipeline.Agents[0].Type = "enrich";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Run.PipelineAsync(_workspace.CreateOrchestrator(new FakeLLMClient()), pipeline));
    }

    [Fact]
    public async Task Large_files_are_split_for_the_first_agent_only()
    {
        var llm = new FakeLLMClient(maxInputChars: 1000).Reply("c1", "c2", "c3", "c4", "c5", "c6", "second");
        var pipeline = _workspace.CreatePipeline("text", 2, ("a", "generate"), ("b", "generate"));
        var events = new List<PipelineEvent>();

        await _workspace.CreateOrchestrator(llm).RunPipelineAsync(
            new string('x', 2000), "t", pipeline, [], evt => { events.Add(evt); return Task.CompletedTask; });

        Assert.Contains(events, e => e.Type == PipelineEventType.ChunkProgress);
        Assert.Equal("prompt of b", llm.Calls[^1].SystemPrompt);
        Assert.Single(llm.Calls, c => c.SystemPrompt == "prompt of b");
    }
}
