using System.Text.Json.Serialization;
using AgentController.Models;
using AgentController.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentController.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PipelinesController : ControllerBase
{
    private readonly IPromptLoader _promptLoader;

    public PipelinesController(IPromptLoader promptLoader)
    {
        _promptLoader = promptLoader;
    }

    [HttpGet]
    public IActionResult GetAll() => Ok(_promptLoader.GetPipelines());

    [HttpGet("{name}")]
    public IActionResult Get(string name)
    {
        try
        {
            var pipeline = _promptLoader.GetPipeline(name);
            var promptFiles = _promptLoader.GetPromptFiles(name);
            return Ok(new { pipeline, promptFiles });
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { error = $"Pipeline '{name}' not found" });
        }
    }

    [HttpGet("{name}/prompts/{file}")]
    public IActionResult GetPrompt(string name, string file)
    {
        try
        {
            var content = _promptLoader.LoadPrompt(name, file);
            return Ok(new { file, content });
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreatePipelineRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || req.Agents.Count == 0)
            return BadRequest(new { error = "Name and at least one agent are required" });

        try
        {
            var pipeline = new PipelineInfo
            {
                Name = req.Name,
                DisplayName = req.DisplayName,
                Description = req.Description,
                OutputType = req.OutputType,
                MaxCorrectionLoops = req.MaxCorrectionLoops,
                Agents = req.Agents.Select((a, i) =>
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
                }).ToList()
            };

            var prompts = pipeline.Agents.ToDictionary(
                a => a.PromptFile,
                a => req.Agents.ElementAt(a.Step - 1).Prompt
            );

            return Ok(_promptLoader.CreatePipeline(pipeline, prompts));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{name}")]
    public IActionResult Update(string name, [FromBody] CreatePipelineRequest req)
    {
        try { _promptLoader.GetPipeline(name); }
        catch (FileNotFoundException) { return NotFound(new { error = $"Pipeline '{name}' not found" }); }

        if (req.Agents.Count == 0)
            return BadRequest(new { error = "At least one agent is required" });

        var pipeline = new PipelineInfo
        {
            Name = name,
            DisplayName = req.DisplayName,
            Description = req.Description,
            OutputType = req.OutputType,
            MaxCorrectionLoops = req.MaxCorrectionLoops,
            Agents = req.Agents.Select((a, i) =>
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
            }).ToList()
        };

        var prompts = pipeline.Agents.ToDictionary(
            a => a.PromptFile,
            a => req.Agents.ElementAt(a.Step - 1).Prompt
        );

        return Ok(_promptLoader.UpdatePipeline(pipeline, prompts));
    }

    [HttpDelete("{name}")]
    public IActionResult Delete(string name)
    {
        try
        {
            _promptLoader.DeletePipeline(name);
            return Ok(new { deleted = true });
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { error = $"Pipeline '{name}' not found" });
        }
    }

    [HttpPut("{name}/prompts/{file}")]
    public IActionResult SavePrompt(string name, string file, [FromBody] SavePromptRequest body)
    {
        try
        {
            _promptLoader.SavePrompt(name, file, body.Content);
            return Ok(new { file, saved = true });
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    private static string Slugify(string name) =>
        System.Text.RegularExpressions.Regex
            .Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-")
            .Trim('-');
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
    public string Content { get; set; } = "";
}