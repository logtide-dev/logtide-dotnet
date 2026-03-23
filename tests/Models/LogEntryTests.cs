using Xunit;
using LogTide.SDK.Models;

namespace LogTide.SDK.Tests.Models;

public class LogEntryTests
{
    [Fact]
    public void LogEntry_DefaultsAreCorrect()
    {
        var entry = new LogEntry();
        Assert.Null(entry.SpanId);
        Assert.Null(entry.SessionId);
    }

    [Fact]
    public void ClientOptions_ParsesDsn()
    {
        var opts = ClientOptions.FromDsn("https://lp_mykey@api.logtide.dev");
        Assert.Equal("https://api.logtide.dev", opts.ApiUrl);
        Assert.Equal("lp_mykey", opts.ApiKey);
    }
}
