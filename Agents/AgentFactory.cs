using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

    public Agent Create(string pipelineName, AgentInfo agentInfo)
    {
        var systemPrompt = _promptLoader.LoadPrompt(pipelineName, agentInfo.PromptFile);
        var logger = _loggerFactory.CreateLogger($"Agent.{agentInfo.Role}");

        var preferredProvider = _config[$"AgentConfigs:{agentInfo.Role}:provider"] ?? "anthropic";

        return new Agent(agentInfo.Name, agentInfo.Role, systemPrompt, _clientFactory, preferredProvider, logger);
    }
}