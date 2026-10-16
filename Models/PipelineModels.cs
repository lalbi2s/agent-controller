using System.Text.Json.Serialization;

namespace AgentController.Models;

public class PipelineInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("outputType")]
    public string OutputType { get; set; } = "text";

    [JsonPropertyName("agents")]
    public List<AgentInfo> Agents { get; set; } = [];

    [JsonPropertyName("maxCorrectionLoops")]
    public int MaxCorrectionLoops { get; set; } = 2;
}

public class AgentInfo
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("promptFile")]
    public string PromptFile { get; set; } = string.Empty;

    [JsonPropertyName("outputBehavior")]
    public string OutputBehavior { get; set; } = "";

    [JsonPropertyName("inputSource")]
    public string InputSource { get; set; } = "";

    [JsonPropertyName("correctionTarget")]
    public string? CorrectionTarget { get; set; }
}

public class AgentConfig
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "anthropic";
}

public class ModelInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;
}