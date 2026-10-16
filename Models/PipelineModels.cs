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

public static class AgentTypes
{
    public const string Generate = "generate";
    public const string Review = "review";
    public const string Validate = "validate";

    public static bool IsValid(string type) => type is Generate or Review or Validate;
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

    [JsonPropertyName("type")]
    public string Type { get; set; } = AgentTypes.Generate;
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