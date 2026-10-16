using AgentController.Models;
using AgentController.Services;

namespace AgentController.Agents;

public class AgentFactory
{
    private readonly LLMClientFactory _clientFactory;
    private readonly PromptLoader _promptLoader;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _config;

    public AgentFactory(
        LLMClientFactory clientFactory,
        PromptLoader promptLoader,
        ILoggerFactory loggerFactory,
        IConfiguration config)
    {
        _clientFactory = clientFactory;
        _promptLoader = promptLoader;
        _loggerFactory = loggerFactory;
        _config = config;
    }

    /// <summary>Provider and model come from the run settings, then from the AgentConfigs section of the configuration.</summary>
    public Agent Create(string pipelineName, AgentInfo agentInfo, AgentConfig runConfig)
    {
        var systemPrompt = _promptLoader.LoadPrompt(pipelineName, agentInfo.PromptFile);
        var logger = _loggerFactory.CreateLogger($"Agent.{agentInfo.Role}");

        var provider = FirstFilled(runConfig.Provider, _config[$"AgentConfigs:{agentInfo.Role}:provider"], "anthropic");
        var model = FirstFilled(runConfig.Model, _config[$"AgentConfigs:{agentInfo.Role}:model"], "");

        return new Agent(agentInfo.Name, agentInfo.Role, systemPrompt, _clientFactory, provider, model, logger);
    }

    private static string FirstFilled(string? first, string? second, string last) =>
        !string.IsNullOrWhiteSpace(first) ? first : !string.IsNullOrWhiteSpace(second) ? second : last;
}
