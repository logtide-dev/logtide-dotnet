using System.Text.Json.Serialization;
using LogTide.SDK.Enums;

namespace LogTide.SDK.Models;

/// <summary>
/// Represents a single log entry to be sent to LogTide.
/// </summary>
public class LogEntry
{
    /// <summary>
    /// Service name that generated the log.
    /// </summary>
    [JsonPropertyName("service")]
    public string Service { get; set; } = string.Empty;

    /// <summary>
    /// Log severity level.
    /// </summary>
    [JsonPropertyName("level")]
    public LogLevel Level { get; set; }

    /// <summary>
    /// Log message content.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp in ISO 8601 format.
    /// </summary>
    [JsonPropertyName("time")]
    public string Time { get; set; } = DateTime.UtcNow.ToString("O");

    /// <summary>
    /// Additional metadata attached to the log.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object?> Metadata { get; set; } = new();

    /// <summary>
    /// Optional trace ID for distributed tracing.
    /// </summary>
    [JsonPropertyName("trace_id")]
    public string? TraceId { get; set; }

    /// <summary>
    /// Optional span ID for distributed tracing.
    /// </summary>
    [JsonPropertyName("span_id")]
    public string? SpanId { get; set; }

    /// <summary>
    /// Optional session ID for user session tracking.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}

/// <summary>
/// Internal log entry representation for JSON serialization.
/// </summary>
internal class SerializableLogEntry
{
    [JsonPropertyName("service")]
    public string Service { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public string Level { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("time")]
    public string Time { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }

    [JsonPropertyName("trace_id")]
    public string? TraceId { get; set; }

    [JsonPropertyName("span_id")]
    public string? SpanId { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    public static SerializableLogEntry FromLogEntry(LogEntry entry) => new()
    {
        Service = entry.Service,
        Level = entry.Level.ToApiString(),
        Message = entry.Message,
        Time = entry.Time,
        Metadata = entry.Metadata.Count > 0 ? entry.Metadata : null,
        TraceId = entry.TraceId,
        SpanId = entry.SpanId,
        SessionId = entry.SessionId
    };
}
