using Xunit;
using LogTide.SDK.Tracing;

namespace LogTide.SDK.Tests.Tracing;

public class SpanTests
{
    [Fact]
    public void Span_InitialState_IsCorrect()
    {
        var span = new Span("abc123", "trace123", null, "HTTP GET");
        Assert.Equal("abc123", span.SpanId);
        Assert.Equal("trace123", span.TraceId);
        Assert.Equal("HTTP GET", span.Name);
        Assert.Equal(SpanStatus.Unset, span.Status);
        Assert.False(span.IsFinished);
    }

    [Fact]
    public void Finish_SetsEndTimeAndStatus()
    {
        var span = new Span("a", "b", null, "test");
        span.Finish(SpanStatus.Ok);
        Assert.True(span.IsFinished);
        Assert.Equal(SpanStatus.Ok, span.Status);
        Assert.NotNull(span.EndTime);
    }

    [Fact]
    public void SetAttribute_StoresValue()
    {
        var span = new Span("a", "b", null, "test");
        span.SetAttribute("http.method", "GET");
        Assert.Equal("GET", span.Attributes["http.method"]);
    }

    [Fact]
    public void AddEvent_AppendsToList()
    {
        var span = new Span("a", "b", null, "test");
        span.AddEvent("exception", new Dictionary<string, object?> { ["message"] = "oops" });
        Assert.Single(span.Events);
        Assert.Equal("exception", span.Events[0].Name);
    }
}
