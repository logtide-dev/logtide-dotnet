using Xunit;
using LogTide.SDK.Core;
using LogTide.SDK.Models;
using LogTide.SDK.Tests.Helpers;

namespace LogTide.SDK.Tests.Core;

/// <summary>beforeSend hook and sampling (conformance C22/C23).</summary>
public class HooksTests
{
    private static (LogTideClient client, FakeTransport transport) Create(Action<ClientOptions>? configure = null)
    {
        var opts = new ClientOptions
        {
            ApiUrl = "http://localhost:8080",
            ApiKey = "lp_test_key",
            FlushIntervalMs = 60000,
            Debug = false
        };
        configure?.Invoke(opts);
        var fake = new FakeTransport();
        var client = new LogTideClient(opts, fake, fake);
        return (client, fake);
    }

    [Fact]
    public async Task BeforeSend_CanMutate()
    {
        var (client, fake) = Create(o => o.BeforeSend = entry =>
        {
            entry.Metadata["password"] = "[redacted]";
            return entry;
        });

        client.Info("svc", "login", new Dictionary<string, object?> { ["password"] = "hunter2" });
        await client.FlushAsync();

        Assert.Equal("[redacted]", fake.LogBatches[0][0].Metadata["password"]);
    }

    [Fact]
    public async Task BeforeSend_CanDrop()
    {
        var (client, fake) = Create(o => o.BeforeSend = _ => null);

        client.Info("svc", "dropped");
        await client.FlushAsync();

        Assert.Empty(fake.LogBatches);
        Assert.Equal(0, client.GetMetrics().LogsSent);
    }

    [Fact]
    public async Task BeforeSend_ExceptionKeepsTheEntry()
    {
        var (client, fake) = Create(o => o.BeforeSend = _ => throw new InvalidOperationException("hook bug"));

        client.Info("svc", "survives");
        await client.FlushAsync();

        Assert.Single(Assert.Single(fake.LogBatches));
    }

    [Fact]
    public async Task SampleRateZero_SendsNothing()
    {
        var (client, fake) = Create(o => o.SampleRate = 0.0);

        for (var i = 0; i < 20; i++) client.Info("svc", "nope");
        await client.FlushAsync();

        Assert.Empty(fake.LogBatches);
    }

    [Fact]
    public async Task SampleRateOne_SendsEverything()
    {
        var (client, fake) = Create(o => o.SampleRate = 1.0);

        for (var i = 0; i < 5; i++) client.Info("svc", "yes");
        await client.FlushAsync();

        Assert.Equal(5, fake.LogBatches[0].Count);
    }
}
