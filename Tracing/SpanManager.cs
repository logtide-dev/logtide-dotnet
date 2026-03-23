using System.Collections.Concurrent;

namespace LogTide.SDK.Tracing;

internal sealed class SpanManager
{
    private readonly ConcurrentDictionary<string, Span> _spans = new();

    public Span StartSpan(string name, string traceId, string? parentSpanId = null)
    {
        var span = new Span(W3CTraceContext.GenerateSpanId(), traceId, parentSpanId, name);
        _spans.TryAdd(span.SpanId, span);
        return span;
    }

    public bool TryFinishSpan(string spanId, SpanStatus status, out Span? span)
    {
        if (_spans.TryRemove(spanId, out span))
        {
            span.Finish(status);
            return true;
        }
        return false;
    }
}
