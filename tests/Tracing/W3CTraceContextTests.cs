using Xunit;
using LogTide.SDK.Tracing;

namespace LogTide.SDK.Tests.Tracing;

public class W3CTraceContextTests
{
    [Fact]
    public void Parse_ValidTraceparent_ReturnsIds()
    {
        var result = W3CTraceContext.Parse("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
        Assert.NotNull(result);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", result.Value.TraceId);
        Assert.Equal("00f067aa0ba902b7", result.Value.SpanId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("00-short-00f067aa0ba902b7-01")]
    [InlineData("00-zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz-00f067aa0ba902b7-01")] // non-hex trace ID
    [InlineData("00-4bf92f3577b34da6a3ce929d0e0e4736-GGGGGGGGGGGGGGGG-01")] // uppercase/non-hex span ID
    [InlineData("00-00000000000000000000000000000000-00f067aa0ba902b7-01")] // all-zeros trace ID
    [InlineData("00-4bf92f3577b34da6a3ce929d0e0e4736-0000000000000000-01")] // all-zeros span ID
    public void Parse_InvalidInput_ReturnsNull(string? input)
    {
        Assert.Null(W3CTraceContext.Parse(input));
    }

    [Fact]
    public void Create_ProducesValidFormat()
    {
        var header = W3CTraceContext.Create("4bf92f3577b34da6a3ce929d0e0e4736", "00f067aa0ba902b7");
        Assert.Matches(@"^00-[0-9a-f]{32}-[0-9a-f]{16}-0[01]$", header);
    }

    [Fact]
    public void GenerateTraceId_IsLowercaseHex32()
    {
        var id = W3CTraceContext.GenerateTraceId();
        Assert.Equal(32, id.Length);
        Assert.Matches("^[0-9a-f]{32}$", id);
    }

    [Fact]
    public void GenerateSpanId_IsLowercaseHex16()
    {
        var id = W3CTraceContext.GenerateSpanId();
        Assert.Equal(16, id.Length);
        Assert.Matches("^[0-9a-f]{16}$", id);
    }
}
