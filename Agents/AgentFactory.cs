using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AgentController.Models;
using AgentController.Services;

namespace AgentController.Agents;

public interface IAgentFactory
{
    IAgent Create(string pipelineName, AgentInfo agentInfo, string? promptFileOverride = null);
}

public class AgentFactory : IAgentFactory
{
    private readonly ILLMClientFactory _clientFactory;  
    private readonly IPromptLoader _promptLoader;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _config;            

    public AgentFactory(
        ILLMClientFactory clientFactory,  
        IPromptLoader promptLoader,
        ILoggerFactory loggerFactory,
        IConfiguration config)            
    {
        _clientFactory = clientFactory;
        _promptLoader = promptLoader;
        _loggerFactory = loggerFactory;
        _config = config;
    }

    public IAgent Create(string pipelineName, AgentInfo agentInfo, string? promptFileOverride = null)
    {
        var promptFile = promptFileOverride ?? agentInfo.PromptFile;
        var systemPrompt = _promptLoader.LoadPrompt(pipelineName, promptFile);
        var logger = _loggerFactory.CreateLogger($"Agent.{agentInfo.Role}");

        var preferredProvider = _config[$"AgentConfigs:{agentInfo.Role}:provider"] ?? "anthropic";

        return new AgentBase(agentInfo.Name, agentInfo.Role, systemPrompt, _clientFactory, preferredProvider, logger);
    }
}