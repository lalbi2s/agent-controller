using AgentController.Models;
using Microsoft.AspNetCore.Mvc;

namespace AgentController.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModelsController : ControllerBase
{
    private static readonly List<ModelInfo> AvailableModels =
    [
        // Anthropic
        new() { Id = "claude-haiku-4-5-20251001", Name = "Haiku 4.5",  Provider = "anthropic" },
        new() { Id = "claude-sonnet-4-20250514",   Name = "Sonnet 4",   Provider = "anthropic" },
        new() { Id = "claude-opus-4-20250514",     Name = "Opus 4",     Provider = "anthropic" },

        // GitHub Models
        new() { Id = "gpt-4o-mini", Name = "GPT-4o Mini", Provider = "github" },
        new() { Id = "gpt-4o",      Name = "GPT-4o",      Provider = "github" },
        new() { Id = "o3-mini",     Name = "o3 Mini",     Provider = "github" },
    ];

    [HttpGet]
    public IActionResult Get() => Ok(AvailableModels);

    [HttpGet("/api/agent-defaults")]
    public IActionResult GetAgentDefaults([FromServices] IConfiguration config)
    {
        var defaults = new Dictionary<string, object>();
        var roles = new[] { "extractor", "architect", "reviewer", "validator" };

        foreach (var role in roles)
        {
            defaults[role] = new
            {
                provider = config[$"AgentConfigs:{role}:provider"] ?? "anthropic",
                model = config[$"AgentConfigs:{role}:model"] ?? ""
            };
        }

        return Ok(defaults);
    }
}