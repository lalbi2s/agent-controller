using System.Text.Json.Serialization;

namespace AgentController.Models;

public class ReviewIssue
{
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class ReviewResult
{
    [JsonPropertyName("issues")]
    public List<ReviewIssue> Issues { get; set; } = [];

    [JsonPropertyName("suggestions")]
    public List<string> Suggestions { get; set; } = [];

    public bool HasErrors => Issues.Any(i => i.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
}
