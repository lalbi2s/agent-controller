using System.Text.Json.Serialization;

namespace AgentController.Models;

public class Metric
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Source { get; set; }

    [JsonPropertyName("flow")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Flow { get; set; }

    [JsonPropertyName("imported")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Imported { get; set; }

    [JsonPropertyName("dynamic")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Dynamic { get; set; }
}

public class TestProfile
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "load";

    [JsonPropertyName("maxVUs")]
    public int MaxVUs { get; set; }

    [JsonPropertyName("totalDurationSeconds")]
    public int TotalDurationSeconds { get; set; }

    [JsonPropertyName("rampUpDuration")]
    public string RampUpDuration { get; set; } = string.Empty;

    [JsonPropertyName("sustainDuration")]
    public string SustainDuration { get; set; } = string.Empty;

    [JsonPropertyName("stagesShape")]
    public string StagesShape { get; set; } = "plateau";

    [JsonPropertyName("hasRampDown")]
    public bool HasRampDown { get; set; }
}

public class ExtractorResult
{
    [JsonPropertyName("metrics")]
    public List<Metric> Metrics { get; set; } = [];

    [JsonPropertyName("testProfile")]
    public TestProfile? TestProfile { get; set; }
}
