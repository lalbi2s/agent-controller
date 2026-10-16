using System.Text.Json.Serialization;

namespace AgentController.Models;

public class ValidationResult
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = [];
}
