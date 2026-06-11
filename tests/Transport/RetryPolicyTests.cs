using System.Diagnostics;
using Xunit;
using LogTide.SDK.Core;
using LogTide.SDK.Exceptions;
using LogTide.SDK.Models;
using LogTide.SDK.Tests.Helpers;

namespace LogTide.SDK.Tests.Transport;

/// <summary>
/// Retry policy per spec 002 §6 (conformance C07/C08/C09). Retryable:
/// network errors, 408, 429, 5xx. Permanent client errors (other 4xx) are
/// dropped after the first attempt. Retry-After overrides the backoff.
/// </summary>
public class RetryPolicyTests
{
    private sealed class CountingTransport : LogTide.SDK.Transport.ILogTransport
    {
        public int Calls;
        public Queue<Exception?> Outcomes { get; } = new();

        public Task SendAsync(IReadOnlyList<LogEntry> logs, CancellationToken ct = default)
        {
            Calls++;
            var outcome = Outcomes.Count > 0 ? Outcomes.Dequeue() : null;
            if (outcome != null) throw outcome;
            return Task.CompletedTask;
        }
    }

    private static (LogTideClient client, CountingTransport transport) Create()
    {
        var opts = new ClientOptions
        {
            ApiUrl = "http://localhost:8080",
            ApiKey = "lp_test_key",
            FlushIntervalMs = 60000,
            RetryDelayMs = 1,
            Debug = false
        };
        var transport = new CountingTransport();
        var client = new LogTideClient(opts, transport, null);
        return (client, transport);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(413)]
    public async Task PermanentClientErrors_AreNotRetried(int status)
    {
        var (client, transport) = Create();
        transport.Outcomes.Enqueue(new ApiException(status, $"HTTP {status}"));
        transport.Outcomes.Enqueue(null); // would succeed if (wrongly) retried

        client.Info("svc", "m");
        await client.FlushAsync();

        Assert.Equal(1, transport.Calls);
        Assert.Equal(1, client.GetMetrics().LogsDropped);
        Assert.Equal(0, client.GetMetrics().Retries);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task RetryableStatuses_AreRetried(int status)
    {
        var (client, transport) = Create();
        transport.Outcomes.Enqueue(new ApiException(status, $"HTTP {status}"));

        client.Info("svc", "m");
        await client.FlushAsync();

        Assert.Equal(2, transport.Calls);
        Assert.Equal(1, client.GetMetrics().LogsSent);
    }

    [Fact]
    public async Task NetworkErrors_AreRetried()
    {
        var (client, transport) = Create();
        transport.Outcomes.Enqueue(new HttpRequestException("refused"));

        client.Info("svc", "m");
        await client.FlushAsync();

        Assert.Equal(2, transport.Calls);
    }

    [Fact]
    public async Task RetryAfter_OverridesBackoff()
    {
        var (client, transport) = Create();
        transport.Outcomes.Enqueue(new ApiException(429, "slow down") { RetryAfterMs = 400 });

        client.Info("svc", "m");
        var sw = Stopwatch.StartNew();
        await client.FlushAsync();
        sw.Stop();

        Assert.Equal(2, transport.Calls);
        // RetryDelayMs is 1ms; the 400ms Retry-After must dominate
        Assert.True(sw.ElapsedMilliseconds >= 300, $"elapsed {sw.ElapsedMilliseconds}ms");
    }
}
