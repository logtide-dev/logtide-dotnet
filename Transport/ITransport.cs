using LogTide.SDK.Models;
using LogTide.SDK.Tracing;

namespace LogTide.SDK.Transport;

internal interface ILogTransport
{
    Task SendAsync(IReadOnlyList<LogEntry> logs, CancellationToken ct = default);
}

internal interface ISpanTransport
{
    Task SendSpansAsync(IReadOnlyList<Span> spans, CancellationToken ct = default);
}
