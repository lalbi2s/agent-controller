using System.Text.Json.Serialization;

namespace AgentController.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PipelineEventType
{
    AgentStart,
    AgentLog,
    AgentComplete,
    AgentSkip,
    AgentError,
    Metrics,
    Review,
    CorrectionLoop,
    ChunkProgress,
    Breakpoint,
    Complete,
    Error
}

public class PipelineEvent
{
    [JsonPropertyName("type")]
    public PipelineEventType Type { get; set; }

    [JsonPropertyName("agent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Agent { get; set; }

    [JsonPropertyName("step")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Step { get; set; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
