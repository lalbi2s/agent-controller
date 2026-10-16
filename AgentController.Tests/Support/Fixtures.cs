namespace AgentController.Tests.Support;

public static class Fixtures
{
    /// <summary>The smallest dashboard that passes the Grafana check.</summary>
    public const string ValidDashboard = """
        {
          "uid": "k6-test",
          "title": "k6 test",
          "schemaVersion": 41,
          "__inputs": [ { "name": "DS_PROMETHEUS", "type": "datasource", "pluginId": "prometheus" } ],
          "panels": [
            { "id": 1, "type": "row", "title": "Overview", "gridPos": { "x": 0, "y": 0, "w": 24, "h": 1 } },
            {
              "id": 2, "type": "timeseries", "title": "Duration",
              "gridPos": { "x": 0, "y": 1, "w": 24, "h": 10 },
              "datasource": { "type": "prometheus", "uid": "${DS_PROMETHEUS}" },
              "targets": [ { "refId": "A", "expr": "max(k6_http_req_duration_p95)", "legendFormat": "{{__name__}}" } ]
            }
          ]
        }
        """;

    /// <summary>Path of a file of the repository, found from the solution file (not from the build output).</summary>
    public static string RepositoryFile(string relativePath)
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Agent-Controller.sln")))
            folder = folder.Parent;

        if (folder is null)
            throw new FileNotFoundException($"Agent-Controller.sln was not found above {AppContext.BaseDirectory}");

        return Path.Combine(folder.FullName, relativePath);
    }
}
