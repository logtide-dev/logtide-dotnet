using LogTide.SDK.Breadcrumbs;
using LogTide.SDK.Tracing;

namespace LogTide.SDK.Core;

public sealed class LogTideScope : IDisposable
{
    private static readonly AsyncLocal<LogTideScope?> _current = new();

    public static LogTideScope? Current => _current.Value;

    public string TraceId { get; }
    public string? SpanId { get; internal set; }
    public string? SessionId { get; set; }

    private readonly BreadcrumbBuffer _breadcrumbs = new(maxSize: 50);
    private readonly LogTideScope? _previous;

    private LogTideScope(string traceId)
    {
        TraceId = traceId;
        _previous = _current.Value;
        _current.Value = this;
    }

    public static LogTideScope Create(string? traceId = null) =>
        new(traceId ?? W3CTraceContext.GenerateTraceId());

    public void AddBreadcrumb(Breadcrumb breadcrumb) => _breadcrumbs.Add(breadcrumb);
    public IReadOnlyList<Breadcrumb> GetBreadcrumbs() => _breadcrumbs.GetAll();

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _current.Value = _previous;
    }
}
