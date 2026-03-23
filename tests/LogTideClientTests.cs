using Xunit;
using LogTide.SDK.Core;
using LogTide.SDK.Enums;
using LogTide.SDK.Exceptions;
using LogTide.SDK.Models;
using LogTide.SDK.Tests.Helpers;

namespace LogTide.SDK.Tests;

public class LogTideClientLegacyTests
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
    public void Constructor_ThrowsOnNullOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new LogTideClient(null!, new FakeTransport(), null));
    }

    [Fact]
    public void Log_AddsToBuffer()
    {
        var (client, _) = Create();
        client.Info("test", "Test message");
        var metrics = client.GetMetrics();
        Assert.Equal(0, metrics.LogsSent);
    }

    [Fact]
    public async Task Log_MergesGlobalMetadata()
    {
        var (client, fake) = Create(o =>
            o.GlobalMetadata = new Dictionary<string, object?> { ["env"] = "test", ["version"] = "1.0.0" });
        client.Info("test", "Test message", new Dictionary<string, object?> { ["custom"] = "value" });
        await client.FlushAsync();
        Assert.Single(fake.LogBatches);
        Assert.Equal("test", fake.LogBatches[0][0].Metadata["env"]);
    }

    [Fact]
    public async Task Log_AppliesAutoTraceId_WhenEnabled()
    {
        var (client, fake) = Create(o => o.AutoTraceId = true);
        client.Info("test", "Test message");
        await client.FlushAsync();
        Assert.NotNull(fake.LogBatches[0][0].TraceId);
    }

    [Fact]
    public void GetMetrics_ReturnsClone()
    {
        var (client, _) = Create();
        var metrics1 = client.GetMetrics();
        var metrics2 = client.GetMetrics();
        Assert.NotSame(metrics1, metrics2);
    }

    [Fact]
    public void ResetMetrics_ClearsAllMetrics()
    {
        var (client, _) = Create();
        client.ResetMetrics();
        var metrics = client.GetMetrics();
        Assert.Equal(0, metrics.LogsSent);
        Assert.Equal(0, metrics.LogsDropped);
        Assert.Equal(0, metrics.Errors);
        Assert.Equal(0, metrics.Retries);
        Assert.Equal(0, metrics.AvgLatencyMs);
        Assert.Equal(0, metrics.CircuitBreakerTrips);
    }

    [Fact]
    public void GetCircuitBreakerState_ReturnsClosedInitially()
    {
        var (client, _) = Create();
        Assert.Equal(CircuitState.Closed, client.GetCircuitBreakerState());
    }

    [Fact]
    public async Task Error_WithException_SerializesError()
    {
        var (client, fake) = Create();
        var exception = new InvalidOperationException("Test error");
        client.Error("test", "Error occurred", exception);
        await client.FlushAsync();
        Assert.Single(fake.LogBatches);
        Assert.True(fake.LogBatches[0][0].Metadata.ContainsKey("error"));
    }

    [Fact]
    public async Task Critical_WithException_SerializesError()
    {
        var (client, fake) = Create();
        var exception = new ApplicationException("Critical error",
            new InvalidOperationException("Inner error"));
        client.Critical("test", "Critical error occurred", exception);
        await client.FlushAsync();
        Assert.Single(fake.LogBatches);
        Assert.True(fake.LogBatches[0][0].Metadata.ContainsKey("error"));
    }
}
