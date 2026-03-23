using LogTide.SDK.Breadcrumbs;
using LogTide.SDK.Enums;
using LogTide.SDK.Models;
using LogTide.SDK.Tracing;

namespace LogTide.SDK.Core;

public interface ILogTideClient : IDisposable, IAsyncDisposable
{
    void Log(LogEntry entry);
    void Debug(string service, string message, Dictionary<string, object?>? metadata = null);
    void Info(string service, string message, Dictionary<string, object?>? metadata = null);
    void Warn(string service, string message, Dictionary<string, object?>? metadata = null);
    void Error(string service, string message, Dictionary<string, object?>? metadata = null);
    void Error(string service, string message, Exception exception);
    void Critical(string service, string message, Dictionary<string, object?>? metadata = null);
    void Critical(string service, string message, Exception exception);
    Task FlushAsync(CancellationToken cancellationToken = default);
    Span StartSpan(string name, string? parentSpanId = null);
    void FinishSpan(Span span, SpanStatus status = SpanStatus.Ok);
    void AddBreadcrumb(Breadcrumb breadcrumb);
    ClientMetrics GetMetrics();
    void ResetMetrics();
    CircuitState GetCircuitBreakerState();
}
