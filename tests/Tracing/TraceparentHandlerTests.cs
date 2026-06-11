using Xunit;
using LogTide.SDK.Core;
using LogTide.SDK.Tracing;

namespace LogTide.SDK.Tests.Tracing;

/// <summary>Outbound traceparent injection (conformance C25, spec 005 §2).</summary>
public class TraceparentHandlerTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Traceparent;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            Traceparent = request.Headers.TryGetValues("traceparent", out var values)
                ? string.Join(",", values)
                : null;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }

    private static (HttpClient client, CapturingHandler inner) Create()
    {
        var inner = new CapturingHandler();
        var handler = new LogTideTraceparentHandler { InnerHandler = inner };
        return (new HttpClient(handler), inner);
    }

    [Fact]
    public async Task InjectsFromTheCurrentScope()
    {
        var (client, inner) = Create();
        using var scope = LogTideScope.Create("4bf92f3577b34da6a3ce929d0e0e4736");
        scope.SpanId = "00f067aa0ba902b7";

        await client.GetAsync("http://localhost:1/x");

        Assert.Equal("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", inner.Traceparent);
    }

    [Fact]
    public async Task GeneratesSpanIdWhenScopeHasNone()
    {
        var (client, inner) = Create();
        using var scope = LogTideScope.Create("4bf92f3577b34da6a3ce929d0e0e4736");

        await client.GetAsync("http://localhost:1/x");

        Assert.NotNull(inner.Traceparent);
        Assert.StartsWith("00-4bf92f3577b34da6a3ce929d0e0e4736-", inner.Traceparent);
        var parts = inner.Traceparent!.Split('-');
        Assert.Equal(16, parts[2].Length);
    }

    [Fact]
    public async Task NoopWithoutTraceContext()
    {
        var (client, inner) = Create();

        await client.GetAsync("http://localhost:1/x");

        Assert.Null(inner.Traceparent);
    }

    [Fact]
    public async Task DoesNotOverrideExistingHeader()
    {
        var (client, inner) = Create();
        using var scope = LogTideScope.Create("4bf92f3577b34da6a3ce929d0e0e4736");
        scope.SpanId = "00f067aa0ba902b7";

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:1/x");
        request.Headers.TryAddWithoutValidation(
            "traceparent", "00-" + new string('a', 32) + "-" + new string('b', 16) + "-01");
        await client.SendAsync(request);

        Assert.Contains(new string('a', 32), inner.Traceparent);
    }
}
