using LogTide.SDK.Models;
using LogTide.SDK.Tracing;
using LogTide.SDK.Transport;

namespace LogTide.SDK.Tests.Helpers;

internal sealed class FakeTransport : ILogTransport, ISpanTransport
{
    public List<IReadOnlyList<LogEntry>> LogBatches { get; } = new();
    public List<IReadOnlyList<Span>> SpanBatches { get; } = new();
    public int CallCount => LogBatches.Count;
    public Exception? ThrowOn { get; set; }
    private int _failFirstN;

    public void FailFirstN(int n) => _failFirstN = n;

    public Task SendAsync(IReadOnlyList<LogEntry> logs, CancellationToken ct = default)
    {
        if (_failFirstN > 0) { _failFirstN--; throw ThrowOn ?? new HttpRequestException("fake failure"); }
        if (ThrowOn != null) throw ThrowOn;
        LogBatches.Add(logs);
        return Task.CompletedTask;
    }

    public Task SendSpansAsync(IReadOnlyList<Span> spans, CancellationToken ct = default)
    {
        SpanBatches.Add(spans);
        return Task.CompletedTask;
    }
}
