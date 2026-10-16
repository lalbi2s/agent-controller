using AgentController.Models;
using Microsoft.AspNetCore.Mvc;

namespace AgentController.Controllers;

[ApiController]
public class ModelsController : ControllerBase
{
    private readonly IConfiguration _config;

    public ModelsController(IConfiguration config)
    {
        _config = config;
    }

    // The Models section of the configuration lists the models offered in the interface
    [HttpGet("api/models")]
    public IActionResult GetModels() =>
        Ok(_config.GetSection("Models").Get<List<ModelInfo>>() ?? []);

    // The AgentConfigs section gives the provider and model each agent role starts with
    [HttpGet("api/agent-defaults")]
    public IActionResult GetAgentDefaults()
    {
        var defaults = _config.GetSection("AgentConfigs").GetChildren().ToDictionary(
            role => role.Key,
            role => new
            {
                provider = role["provider"] ?? "anthropic",
                model = role["model"] ?? ""
            });

        return Ok(defaults);
    }
}
