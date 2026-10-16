using System.Text.Json.Serialization;

namespace AgentController.Models;

public class Metric
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("flow")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Flow { get; set; }
}

public class ExtractorResult
{
    [JsonPropertyName("metrics")]
    public List<Metric> Metrics { get; set; } = [];
}
