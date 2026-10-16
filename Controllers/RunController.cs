using System.Text;
using System.Text.Json;
using AgentController.Models;
using AgentController.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentController.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RunController : ControllerBase
{
    private readonly IOrchestrator _orchestrator;
    private readonly IPromptLoader _promptLoader;
    private readonly IBreakpointService _breakpointService;
    private readonly ILogger<RunController> _logger;

    public RunController(
        IOrchestrator orchestrator,
        IPromptLoader promptLoader,
        IBreakpointService breakpointService,
        ILogger<RunController> logger)
    {
        _orchestrator = orchestrator;
        _promptLoader = promptLoader;
        _breakpointService = breakpointService;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(500 * 1024 * 1024)]
    public async Task Post(
        List<IFormFile> files,
        [FromForm] string pipeline,
        [FromForm] string? agentConfigs = null,
        [FromForm] string? breakpoints = null)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var runId = _breakpointService.CreateRun();

        try
        {
            var fileContent = await ConcatenateFiles(files);
            var testName = DeriveTestName(files);
            var configs = ParseAgentConfigs(agentConfigs);
            var breakpointSet = ParseBreakpoints(breakpoints);
            var pipelineInfo = _promptLoader.GetPipeline(pipeline);

            _logger.LogInformation("Starting pipeline {Pipeline} for {FileCount} file(s) (run {RunId})",
                pipeline, files.Count, runId);

            await _orchestrator.RunPipelineAsync(
                fileContent, testName, pipelineInfo, configs,
                async evt => await WriteSseEvent(evt),
                HttpContext.RequestAborted, runId, breakpointSet);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Pipeline cancelled (run {RunId})", runId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline error (run {RunId})", runId);
            await WriteSseEvent(new PipelineEvent
            {
                Type = PipelineEventType.Error,
                Message = ex.Message
            });
        }
        finally
        {
            _breakpointService.CleanupRun(runId);
        }
    }

    [HttpPost("resume/{runId}")]
    public IActionResult Resume(string runId)
    {
        _breakpointService.Resume(runId);
        return Ok();
    }

    private static async Task<string> ConcatenateFiles(List<IFormFile> files)
    {
        var sb = new StringBuilder();
        foreach (var file in files)
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync();
            sb.AppendLine($"--- FILE: {file.FileName} ---");
            sb.AppendLine(content);
            sb.AppendLine($"--- END FILE ---");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string DeriveTestName(List<IFormFile> files)
    {
        if (files.Count == 1)
            return Path.GetFileNameWithoutExtension(files[0].FileName);
        return $"{files.Count}-files";
    }

    private static Dictionary<string, AgentConfig> ParseAgentConfigs(string? raw)
    {
        var configs = new Dictionary<string, AgentConfig>();
        if (string.IsNullOrEmpty(raw)) return configs;

        var parsed = JsonSerializer.Deserialize<List<AgentConfig>>(raw) ?? [];
        foreach (var c in parsed)
            configs[c.Role] = c;
        return configs;
    }

    private static HashSet<string>? ParseBreakpoints(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;

        var list = JsonSerializer.Deserialize<List<string>>(raw) ?? [];
        return list.Count > 0
            ? new HashSet<string>(list, StringComparer.OrdinalIgnoreCase)
            : null;
    }

    private async Task WriteSseEvent(PipelineEvent evt)
    {
        var json = JsonSerializer.Serialize(evt, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }
}
