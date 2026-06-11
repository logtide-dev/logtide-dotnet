using LogTide.SDK.Core;

namespace LogTide.SDK.Tracing;

/// <summary>
/// DelegatingHandler that injects the W3C traceparent header on outbound
/// requests, propagating the current trace context to downstream services
/// (spec 005 §2, conformance C25):
/// <code>
/// var http = new HttpClient(new LogTideTraceparentHandler
/// {
///     InnerHandler = new HttpClientHandler()
/// });
/// </code>
/// Requests with no active <see cref="LogTideScope"/>, or with a traceparent
/// header already set, pass through untouched. A scope without a span id
/// gets a fresh one for this hop.
/// </summary>
public sealed class LogTideTraceparentHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains("traceparent"))
        {
            var scope = LogTideScope.Current;
            if (scope != null)
            {
                var spanId = scope.SpanId ?? W3CTraceContext.GenerateSpanId();
                request.Headers.TryAddWithoutValidation(
                    "traceparent", W3CTraceContext.Create(scope.TraceId, spanId));
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
