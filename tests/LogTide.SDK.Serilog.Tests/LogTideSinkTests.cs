using Xunit;
using NSubstitute;
using Serilog.Events;
using Serilog.Parsing;
using LogTide.SDK.Core;
using LogTide.SDK.Enums;
using LogTide.SDK.Models;
using LogTide.SDK.Serilog;

namespace LogTide.SDK.Tests.Serilog;

public class LogTideSinkTests
{
    [Fact]
    public void Emit_MapsLevelsCorrectly()
    {
        var client = Substitute.For<ILogTideClient>();
        var sink = new LogTideSink(client, "test-svc");

        var levels = new[]
        {
            LogEventLevel.Verbose,
            LogEventLevel.Debug,
            LogEventLevel.Information,
            LogEventLevel.Warning,
            LogEventLevel.Error,
            LogEventLevel.Fatal,
        };

        foreach (var serilogLevel in levels)
        {
            var evt = new LogEvent(DateTimeOffset.UtcNow, serilogLevel, null,
                MessageTemplate.Empty, Array.Empty<LogEventProperty>());
            sink.Emit(evt);
        }

        client.Received(6).Log(Arg.Any<LogEntry>());
    }

    [Fact]
    public void Emit_IncludesExceptionInMetadata()
    {
        LogEntry? captured = null;
        var client = Substitute.For<ILogTideClient>();
        client.When(c => c.Log(Arg.Any<LogEntry>()))
              .Do(ci => captured = ci.Arg<LogEntry>());

        var sink = new LogTideSink(client, "svc");
        var ex = new InvalidOperationException("boom");
        var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, ex,
            MessageTemplate.Empty, Array.Empty<LogEventProperty>());

        sink.Emit(evt);

        Assert.NotNull(captured);
        Assert.True(captured!.Metadata.ContainsKey("error"));
    }

    [Fact]
    public void Emit_MapsStructuredPropertiesToMetadata()
    {
        LogEntry? captured = null;
        var client = Substitute.For<ILogTideClient>();
        client.When(c => c.Log(Arg.Any<LogEntry>()))
              .Do(ci => captured = ci.Arg<LogEntry>());

        var sink = new LogTideSink(client, "svc");
        var props = new[] { new LogEventProperty("UserId", new ScalarValue(42)) };
        var parser = new MessageTemplateParser();
        var template = parser.Parse("Hello {UserId}");
        var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            template, props);

        sink.Emit(evt);

        Assert.Equal(42, captured!.Metadata["UserId"]);
    }
}
