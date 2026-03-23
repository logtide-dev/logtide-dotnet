using Xunit;
using LogTide.SDK.Tracing;

namespace LogTide.SDK.Tests.Tracing;

public class SpanManagerTests
{
    [Fact]
    public void StartSpan_ReturnsSpanWithCorrectFields()
    {
        var mgr = new SpanManager();
        var span = mgr.StartSpan("test", "trace123");
        Assert.Equal("trace123", span.TraceId);
        Assert.Equal("test", span.Name);
        Assert.NotEmpty(span.SpanId);
    }

    [Fact]
    public void FinishSpan_RemovesFromActive()
    {
        var mgr = new SpanManager();
        var span = mgr.StartSpan("test", "t");
        Assert.True(mgr.TryFinishSpan(span.SpanId, SpanStatus.Ok, out _));
        Assert.False(mgr.TryFinishSpan(span.SpanId, SpanStatus.Ok, out _));
    }

    [Fact]
    public void TryFinishSpan_UnknownId_ReturnsFalse()
    {
        var mgr = new SpanManager();
        Assert.False(mgr.TryFinishSpan("nonexistent", SpanStatus.Ok, out _));
    }
}
