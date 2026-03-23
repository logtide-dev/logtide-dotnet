using System.Text;
using System.Text.Json;
using LogTide.SDK.Exceptions;
using LogTide.SDK.Internal;
using LogTide.SDK.Tracing;

namespace LogTide.SDK.Transport;

internal sealed class OtlpHttpTransport : ISpanTransport
{
    private readonly HttpClient _httpClient;
    private readonly string _serviceName;

    public OtlpHttpTransport(HttpClient httpClient, string serviceName)
    {
        _httpClient = httpClient;
        _serviceName = serviceName;
    }

    public async Task SendSpansAsync(IReadOnlyList<Span> spans, CancellationToken ct = default)
    {
        var payload = BuildOtlpPayload(spans);
        var json = JsonSerializer.Serialize(payload, JsonConfig.Options);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/otlp/traces", content, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new ApiException((int)response.StatusCode,
                await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
    }

    private object BuildOtlpPayload(IReadOnlyList<Span> spans) => new
    {
        resourceSpans = new[]
        {
            new
            {
                resource = new
                {
                    attributes = new[] { new { key = "service.name", value = new { stringValue = _serviceName } } }
                },
                scopeSpans = new[]
                {
                    new { spans = spans.Select(ToOtlp).ToArray() }
                }
            }
        }
    };

    private static object ToOtlp(Span s) => new
    {
        traceId = s.TraceId,
        spanId = s.SpanId,
        parentSpanId = s.ParentSpanId,
        name = s.Name,
        startTimeUnixNano = s.StartTime.ToUnixTimeMilliseconds() * 1_000_000L,
        endTimeUnixNano = (s.EndTime ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds() * 1_000_000L,
        status = new { code = (int)s.Status },
        attributes = s.Attributes.Select(kv => new { key = kv.Key, value = new { stringValue = kv.Value?.ToString() } }).ToArray(),
        events = s.Events.Select(e => new { name = e.Name, timeUnixNano = e.Timestamp.ToUnixTimeMilliseconds() * 1_000_000L }).ToArray()
    };
}
