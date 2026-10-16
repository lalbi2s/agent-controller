using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace AgentController.Services;

public class LLMClientFactory : ILLMClientFactory
{
    private readonly IReadOnlyList<ILLMClient> _clients;
    private readonly ILogger<LLMClientFactory> _logger;

    public LLMClientFactory(IEnumerable<ILLMClient> clients, ILogger<LLMClientFactory> logger)
    {
        _clients = clients.ToList();
        _logger = logger;

        foreach (var c in _clients)
            _logger.LogInformation("LLM provider registered: {Provider} — available: {Available}", c.ProviderName, c.IsAvailable);
    }

    /// <inheritdoc/>
    public ILLMClient GetClient(string preferredProviderOrModel)
    {
        string targetProvider = preferredProviderOrModel;
        var lowerInput = preferredProviderOrModel?.ToLower() ?? "";

        // Claude models: claude-*
        // OpenAI models: gpt-*, o1, o3, o4
        if (lowerInput.Contains("claude"))
        {
            targetProvider = "Anthropic";
        }
        else if (lowerInput.Contains("gpt") || System.Text.RegularExpressions.Regex.IsMatch(lowerInput, @"\bo\d"))
        {
            targetProvider = "GitHub";
        }

        // 2. Exact match AND available
        var preferred = _clients.FirstOrDefault(c => 
            string.Equals(c.ProviderName, targetProvider, StringComparison.OrdinalIgnoreCase) && c.IsAvailable);
        
        if (preferred != null)
            return preferred;

        // 3. Provider exists but is unavailable → log and fallback
        var exists = _clients.FirstOrDefault(c => 
            string.Equals(c.ProviderName, targetProvider, StringComparison.OrdinalIgnoreCase));
        
        if (exists != null)
            _logger.LogWarning("Provider '{Provider}' is registered but unavailable (missing/invalid API key). Looking for a fallback...", targetProvider);
        else
            _logger.LogWarning("Provider/Model '{Target}' not matched exactly. Looking for a fallback...", preferredProviderOrModel);

        // 4. Any available provider
        var fallback = _clients.FirstOrDefault(c => c.IsAvailable);
        if (fallback != null)
        {
            _logger.LogWarning("Falling back to provider '{Fallback}' instead of '{Preferred}'.", fallback.ProviderName, targetProvider);
            return fallback;
        }

        // 5. Nothing available
        throw new InvalidOperationException(
            $"No LLM providers are available. Registered: [{string.Join(", ", _clients.Select(c => $"{c.ProviderName}(available={c.IsAvailable})"))}]");
    }

    /// <inheritdoc/>
    public ILLMClient? GetFallbackClient(string excludeProvider)
        => _clients.FirstOrDefault(c => c.ProviderName != excludeProvider && c.IsAvailable);
}