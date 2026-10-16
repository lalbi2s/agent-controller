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
        new() { Id = "claude-haiku-4-5-20251001", Name = "Haiku 4.5",  Tier = "fast",      Provider = "anthropic" },
        new() { Id = "claude-sonnet-4-20250514",   Name = "Sonnet 4",   Tier = "balanced",  Provider = "anthropic" },
        new() { Id = "claude-opus-4-20250514",     Name = "Opus 4",     Tier = "powerful",  Provider = "anthropic" },

        // GitHub Models
        new() { Id = "gpt-4o-mini", Name = "GPT-4o Mini", Tier = "fast",     Provider = "github" },
        new() { Id = "gpt-4o",      Name = "GPT-4o",      Tier = "balanced", Provider = "github" },
        new() { Id = "o3-mini",     Name = "o3 Mini",     Tier = "powerful", Provider = "github" },
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