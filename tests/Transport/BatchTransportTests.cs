using Xunit;
using LogTide.SDK.Exceptions;
using LogTide.SDK.Models;
using LogTide.SDK.Tests.Helpers;
using LogTide.SDK.Transport;

namespace LogTide.SDK.Tests.Transport;

public class BatchTransportTests
{
    private static ClientOptions Opts(int batchSize = 100, int flushMs = 60000) => new()
    {
        ApiUrl = "http://localhost", ApiKey = "k",
        BatchSize = batchSize, FlushIntervalMs = flushMs,
        MaxRetries = 0, RetryDelayMs = 0
    };

    [Fact]
    public async Task Enqueue_TriggersBatchFlush_WhenBatchSizeReached()
    {
        var fake = new FakeTransport();
        await using var transport = new BatchTransport(fake, fake, Opts(batchSize: 2));

        var e1 = new LogEntry { Service = "s", Message = "1" };
        var e2 = new LogEntry { Service = "s", Message = "2" };
        transport.Enqueue(e1);
        transport.Enqueue(e2);
        await transport.FlushAsync();

        Assert.Single(fake.LogBatches);
        Assert.Equal(2, fake.LogBatches[0].Count);
    }

    [Fact]
    public async Task FlushAsync_EmptyBuffer_DoesNothing()
    {
        var fake = new FakeTransport();
        await using var transport = new BatchTransport(fake, fake, Opts());
        await transport.FlushAsync();
        Assert.Empty(fake.LogBatches);
    }

    [Fact]
    public async Task Enqueue_DropsLog_WhenBufferFull()
    {
        var fake = new FakeTransport();
        var opts = Opts();
        opts.MaxBufferSize = 2;
        await using var transport = new BatchTransport(fake, fake, opts);

        transport.Enqueue(new LogEntry());
        transport.Enqueue(new LogEntry());
        Assert.Throws<BufferFullException>(() => transport.Enqueue(new LogEntry()));
    }

    [Fact]
    public async Task SendAsync_RetriesOnTransientFailure_ThenSucceeds()
    {
        var fake = new FakeTransport();
        fake.FailFirstN(2);
        var opts = Opts();
        opts.MaxRetries = 3;
        opts.RetryDelayMs = 0;
        await using var transport = new BatchTransport(fake, fake, opts);

        transport.Enqueue(new LogEntry { Service = "svc", Message = "m" });
        await transport.FlushAsync();

        Assert.Single(fake.LogBatches);
        Assert.Equal(2, transport.GetMetrics().Retries);
    }

    [Fact]
    public async Task SendAsync_ExhaustsRetries_DropsLogs()
    {
        var fake = new FakeTransport();
        fake.FailFirstN(10);
        var opts = Opts();
        opts.MaxRetries = 2;
        opts.RetryDelayMs = 0;
        await using var transport = new BatchTransport(fake, fake, opts);

        transport.Enqueue(new LogEntry { Service = "svc", Message = "m" });
        await transport.FlushAsync();

        Assert.Empty(fake.LogBatches);
        Assert.Equal(1, transport.GetMetrics().LogsDropped);
    }
}
