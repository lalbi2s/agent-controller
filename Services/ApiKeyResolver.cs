namespace AgentController.Services;

public class ApiKeyResolver
{
    private static readonly Dictionary<string, string> ConfigPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["anthropic"] = "Anthropic:ApiKey",
        ["openai"] = "OpenAI:ApiKey",
    };

    private readonly IConfiguration _config;

    public ApiKeyResolver(IConfiguration config)
    {
        _config = config;
    }

    public static IEnumerable<string> Providers => ConfigPaths.Keys;

    public string? GetKey(string provider)
    {
        var key = _config[ConfigPaths[provider]];
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }
}
