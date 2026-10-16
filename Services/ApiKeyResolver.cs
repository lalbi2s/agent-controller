namespace AgentController.Services;

/// <summary>
/// Finds the API key of a provider. Order: key saved in the interface, then the configuration
/// (environment variable, then user secrets, then appsettings.json, as ordered by .NET).
/// </summary>
public class ApiKeyResolver
{
    private static readonly Dictionary<string, string> ConfigPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["anthropic"] = "Anthropic:ApiKey",
        ["openai"] = "OpenAI:ApiKey",
    };

    private readonly ApiKeyStore _store;
    private readonly IConfiguration _config;

    public ApiKeyResolver(ApiKeyStore store, IConfiguration config)
    {
        _store = store;
        _config = config;
    }

    public static IEnumerable<string> Providers => ConfigPaths.Keys;

    public static bool IsKnownProvider(string provider) => ConfigPaths.ContainsKey(provider);

    public string? GetKey(string provider) => GetKeyAndSource(provider).Key;

    public (string? Key, string Source) GetKeyAndSource(string provider)
    {
        var saved = _store.Get(provider.ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(saved))
            return (saved, "ui");

        var configured = _config[ConfigPaths[provider]];
        return string.IsNullOrWhiteSpace(configured) ? (null, "none") : (configured.Trim(), "config");
    }
}
