namespace LogTide.SDK.Tracing;

public sealed class Span
{
    public string SpanId { get; }
    public string TraceId { get; }
    public string? ParentSpanId { get; }
    public string Name { get; set; }
    public DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndTime { get; private set; }
    public SpanStatus Status { get; private set; } = SpanStatus.Unset;
    public Dictionary<string, object?> Attributes { get; } = new();
    public List<SpanEvent> Events { get; } = new();
    public bool IsFinished => EndTime.HasValue;

    public Span(string spanId, string traceId, string? parentSpanId, string name)
    {
        SpanId = spanId;
        TraceId = traceId;
        ParentSpanId = parentSpanId;
        Name = name;
    }

    public void SetStatus(SpanStatus status) => Status = status;
    public void SetAttribute(string key, object? value) => Attributes[key] = value;

    public void AddEvent(string name, Dictionary<string, object?>? attrs = null)
        => Events.Add(new SpanEvent(name, DateTimeOffset.UtcNow, attrs ?? new()));

    public void Finish(SpanStatus status = SpanStatus.Ok)
    {
        Status = status;
        EndTime = DateTimeOffset.UtcNow;
    }
}

public sealed class SpanEvent
{
    public string Name { get; }
    public DateTimeOffset Timestamp { get; }
    public Dictionary<string, object?> Attributes { get; }

    public SpanEvent(string name, DateTimeOffset ts, Dictionary<string, object?> attrs)
    {
        Name = name;
        Timestamp = ts;
        Attributes = attrs;
    }
}

public enum SpanStatus { Unset, Ok, Error }
