using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AgentController.Models;
using AgentController.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentController.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PipelinesController : ControllerBase
{
    private readonly PromptLoader _promptLoader;

    public PipelinesController(PromptLoader promptLoader)
    {
        _promptLoader = promptLoader;
    }

    [HttpGet]
    public IActionResult GetAll() => Ok(_promptLoader.GetPipelines());

    [HttpGet("{name}")]
    public IActionResult Get(string name) =>
        Handle(() => Ok(_promptLoader.GetPipeline(name)));

    [HttpGet("{name}/prompts/{file}")]
    public IActionResult GetPrompt(string name, string file) =>
        Handle(() => Ok(new { file, content = _promptLoader.LoadPrompt(name, file) }));

    [HttpPost]
    public IActionResult Create([FromBody] CreatePipelineRequest req) =>
        Handle(() =>
        {
            var (pipeline, prompts) = BuildPipeline(req.Name, req);
            return Ok(_promptLoader.CreatePipeline(pipeline, prompts));
        });

    [HttpPut("{name}")]
    public IActionResult Update(string name, [FromBody] CreatePipelineRequest req) =>
        Handle(() =>
        {
            var (pipeline, prompts) = BuildPipeline(name, req);
            return Ok(_promptLoader.UpdatePipeline(pipeline, prompts));
        });

    [HttpDelete("{name}")]
    public IActionResult Delete(string name) =>
        Handle(() =>
        {
            _promptLoader.DeletePipeline(name);
            return Ok(new { deleted = true });
        });

    [HttpPut("{name}/prompts/{file}")]
    public IActionResult SavePrompt(string name, string file, [FromBody] SavePromptRequest body) =>
        Handle(() =>
        {
            _promptLoader.SavePrompt(name, file, body.Content);
            return Ok(new { file, saved = true });
        });

    private IActionResult Handle(Func<IActionResult> action)
    {
        try
        {
            return action();
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    private static (PipelineInfo Pipeline, Dictionary<string, string> Prompts) BuildPipeline(
        string name, CreatePipelineRequest req)
    {
        if (req.Agents.Count == 0)
            throw new ArgumentException("At least one agent is required");

        var agents = req.Agents.Select((a, i) =>
        {
            var role = string.IsNullOrWhiteSpace(a.Role) ? Slugify(a.Name) : a.Role;
            return new AgentInfo
            {
                Role = role,
                Name = a.Name,
                Description = a.Description,
                Step = i + 1,
                PromptFile = $"{role}.md",
                OutputBehavior = a.OutputBehavior,
                InputSource = a.InputSource,
                CorrectionTarget = a.CorrectionTarget
            };
        }).ToList();

        if (agents.Select(a => a.Role).Distinct().Count() != agents.Count)
            throw new ArgumentException("Each agent needs a different name");

        var pipeline = new PipelineInfo
        {
            Name = name,
            DisplayName = req.DisplayName,
            Description = req.Description,
            OutputType = req.OutputType,
            MaxCorrectionLoops = req.MaxCorrectionLoops,
            Agents = agents
        };

        var prompts = agents
            .Zip(req.Agents, (info, request) => (info.PromptFile, request.Prompt))
            .ToDictionary(pair => pair.PromptFile, pair => pair.Prompt);

        return (pipeline, prompts);
    }

    private static string Slugify(string name) =>
        Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
}

public class CreatePipelineRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("outputType")]
    public string OutputType { get; set; } = "text";

    [JsonPropertyName("maxCorrectionLoops")]
    public int MaxCorrectionLoops { get; set; } = 2;

    [JsonPropertyName("agents")]
    public List<CreateAgentRequest> Agents { get; set; } = [];
}

public class CreateAgentRequest
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    [JsonPropertyName("outputBehavior")]
    public string OutputBehavior { get; set; } = "";

    [JsonPropertyName("inputSource")]
    public string InputSource { get; set; } = "";

    [JsonPropertyName("correctionTarget")]
    public string? CorrectionTarget { get; set; }
}

public class SavePromptRequest
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}
