using Xunit;
using LogTide.SDK.Models;
using LogTide.SDK.Tests.Helpers;
using LogTide.SDK.Transport;

namespace LogTide.SDK.Tests.Transport;

public class BatchTransportBugFixTests
{
    private static ClientOptions Opts(int batchSize = 100, int flushMs = 60000) => new()
    {
        ApiUrl = "http://localhost", ApiKey = "k",
        BatchSize = batchSize, FlushIntervalMs = flushMs,
        MaxRetries = 0, RetryDelayMs = 0
    };

    [Fact]
    public async Task DisposeAsync_FlushesBufferedLogs()
    {
        var fake = new FakeTransport();
        var transport = new BatchTransport(fake, fake, Opts());
        transport.Enqueue(new LogEntry { Service = "s", Message = "buffered" });

        await transport.DisposeAsync();

        Assert.Single(fake.LogBatches);
        Assert.Equal("buffered", fake.LogBatches[0][0].Message);
    }

    [Fact]
    public void Dispose_FlushesBufferedLogs()
    {
        var fake = new FakeTransport();
        var transport = new BatchTransport(fake, fake, Opts());
        transport.Enqueue(new LogEntry { Service = "s", Message = "buffered" });

        transport.Dispose();

        Assert.Single(fake.LogBatches);
    }

    [Fact]
    public async Task CircuitBreakerOpen_DoesNotDoubleCountDrops()
    {
        var fake = new FakeTransport();
        fake.FailFirstN(100); // always fail
        var opts = Opts();
        opts.MaxRetries = 0;
        opts.RetryDelayMs = 0;
        opts.CircuitBreakerThreshold = 1;
        await using var transport = new BatchTransport(fake, fake, opts);

        // First log exhausts retries, trips circuit breaker
        transport.Enqueue(new LogEntry { Service = "s", Message = "1" });
        await transport.FlushAsync();

        // Second log should be blocked by circuit breaker
        transport.Enqueue(new LogEntry { Service = "s", Message = "2" });
        await transport.FlushAsync();

        var metrics = transport.GetMetrics();
        // Each log should be counted exactly once as dropped
        Assert.Equal(2, metrics.LogsDropped);
    }

    [Fact]
    public async Task RetryExhaustion_RecordsFailureOnce()
    {
        var fake = new FakeTransport();
        fake.FailFirstN(100);
        var opts = Opts();
        opts.MaxRetries = 2;
        opts.RetryDelayMs = 0;
        opts.CircuitBreakerThreshold = 10; // high threshold so CB doesn't trip
        await using var transport = new BatchTransport(fake, fake, opts);

        transport.Enqueue(new LogEntry { Service = "s", Message = "m" });
        await transport.FlushAsync();

        var metrics = transport.GetMetrics();
        // Should have 3 errors (initial + 2 retries), 2 retries, 1 dropped
        Assert.Equal(3, metrics.Errors);
        Assert.Equal(2, metrics.Retries);
        Assert.Equal(1, metrics.LogsDropped);
    }
}
