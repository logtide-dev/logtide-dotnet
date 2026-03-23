using Xunit;
using LogTide.SDK.Breadcrumbs;
using LogTide.SDK.Core;
using LogTide.SDK.Models;
using LogTide.SDK.Tests.Helpers;

namespace LogTide.SDK.Tests.Core;

public class LogTideClientTests
{
    private static (LogTideClient client, FakeTransport transport) Create(Action<ClientOptions>? configure = null)
    {
        var opts = new ClientOptions { ApiUrl = "http://localhost", ApiKey = "k", FlushIntervalMs = 60000 };
        configure?.Invoke(opts);
        var fake = new FakeTransport();
        var client = new LogTideClient(opts, fake, fake);
        return (client, fake);
    }

    [Fact]
    public async Task Log_EnrichesFromAmbientScope()
    {
        var (client, fake) = Create();
        using var scope = LogTideScope.Create("trace-abc");
        client.Info("svc", "hello");
        await client.FlushAsync();
        Assert.Single(fake.LogBatches);
        Assert.Equal("trace-abc", fake.LogBatches[0][0].TraceId);
    }

    [Fact]
    public async Task Log_MergesGlobalMetadata()
    {
        var (client, fake) = Create(o => o.GlobalMetadata = new() { ["env"] = "test" });
        client.Info("svc", "msg");
        await client.FlushAsync();
        Assert.Equal("test", fake.LogBatches[0][0].Metadata["env"]);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new LogTideClient(null!, new FakeTransport(), null));
    }

    [Fact]
    public void StartSpan_ReturnSpanWithAmbientTraceId()
    {
        var (client, _) = Create();
        using var scope = LogTideScope.Create("my-trace");
        var span = client.StartSpan("HTTP GET /test");
        Assert.Equal("my-trace", span.TraceId);
        Assert.Equal("HTTP GET /test", span.Name);
    }

    [Fact]
    public void AddBreadcrumb_StoredInCurrentScope()
    {
        var (client, _) = Create();
        using var scope = LogTideScope.Create("t");
        client.AddBreadcrumb(new Breadcrumb { Message = "btn click" });
        Assert.Single(scope.GetBreadcrumbs());
    }

    [Fact]
    public void GetMetrics_ReturnsClone()
    {
        var (client, _) = Create();
        var m1 = client.GetMetrics();
        var m2 = client.GetMetrics();
        Assert.NotSame(m1, m2);
    }
}
