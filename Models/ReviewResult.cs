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

    [JsonIgnore]
    public IEnumerable<ReviewIssue> Errors =>
        Issues.Where(i => string.Equals(i.Severity, "error", StringComparison.OrdinalIgnoreCase));

    public bool HasErrors => Errors.Any();
}
