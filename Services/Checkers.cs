namespace AgentController.Services;

/// <summary>The checks a pipeline can run in code, without calling an LLM.</summary>
public static class Checkers
{
    public const string GrafanaPrometheusDashboard = "grafana-prometheus-dashboard";

    private static readonly Dictionary<string, Func<string, List<string>>> All = new()
    {
        [GrafanaPrometheusDashboard] = GrafanaDashboardChecker.Check,
    };

    public static IEnumerable<string> Names => All.Keys;

    public static bool Exists(string? name) => name is not null && All.ContainsKey(name);

    /// <summary>Returns the problems found in the output. An empty list means it passed.</summary>
    public static List<string> Run(string name, string output) => All[name](output);
}
