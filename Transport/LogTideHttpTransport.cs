using System.Text;
using System.Text.Json;
using LogTide.SDK.Exceptions;
using LogTide.SDK.Internal;
using LogTide.SDK.Models;

namespace LogTide.SDK.Transport;

internal sealed class LogTideHttpTransport : ILogTransport
{
    private readonly HttpClient _httpClient;

    public LogTideHttpTransport(HttpClient httpClient) => _httpClient = httpClient;

    public async Task SendAsync(IReadOnlyList<LogEntry> logs, CancellationToken ct = default)
    {
        var payload = new { logs = logs.Select(SerializableLogEntry.FromLogEntry) };
        var json = JsonSerializer.Serialize(payload, JsonConfig.Options);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/api/v1/ingest", content, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new ApiException((int)response.StatusCode,
                await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
    }
}
