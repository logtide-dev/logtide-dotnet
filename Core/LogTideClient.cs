using System.Text.Json;
using LogTide.SDK.Breadcrumbs;
using LogTide.SDK.Enums;
using LogTide.SDK.Exceptions;
using LogTide.SDK.Internal;
using LogTide.SDK.Models;
using LogTide.SDK.Tracing;
using LogTide.SDK.Transport;

namespace LogTide.SDK.Core;

public sealed class LogTideClient : ILogTideClient
{
    private readonly ClientOptions _options;
    private readonly BatchTransport _transport;
    private readonly SpanManager _spanManager = new();
    private readonly HttpClient _queryHttpClient;
    private readonly bool _ownsHttpClient;
    private int _disposed; // 0 = not disposed, 1 = disposed; accessed via Interlocked

    /// <summary>
    /// Creates a new LogTide client from a DSN string.
    /// </summary>
    public static LogTideClient FromDsn(string dsn, ClientOptions? baseOptions = null)
    {
        var opts = baseOptions ?? new ClientOptions();
        opts.Dsn = dsn;
        opts.Resolve();
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(opts.ApiUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(opts.HttpTimeoutSeconds)
        };
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", opts.ApiKey);
        return new LogTideClient(opts,
            new LogTideHttpTransport(httpClient),
            new OtlpHttpTransport(httpClient, opts.ServiceName),
            httpClient,
            ownsHttpClient: true);
    }

    /// <summary>
    /// Creates a new LogTide client with an IHttpClientFactory (for DI scenarios).
    /// </summary>
    public LogTideClient(ClientOptions options, IHttpClientFactory httpClientFactory)
    {
        var httpClient = httpClientFactory.CreateClient("LogTide");
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Resolve();
        _transport = new BatchTransport(
            new LogTideHttpTransport(httpClient),
            new OtlpHttpTransport(httpClient, options.ServiceName),
            options);
        _queryHttpClient = httpClient;
        _ownsHttpClient = false; // factory manages lifetime
        foreach (var integration in options.Integrations)
            integration.Setup(this);
    }

    /// <summary>
    /// Creates a new LogTide client for testing or direct construction.
    /// </summary>
    internal LogTideClient(ClientOptions options, ILogTransport logTransport, ISpanTransport? spanTransport,
        HttpClient? queryHttpClient = null, bool ownsHttpClient = false)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Resolve();
        _transport = new BatchTransport(logTransport, spanTransport, options);
        _ownsHttpClient = ownsHttpClient || queryHttpClient == null;
        _queryHttpClient = queryHttpClient ?? new HttpClient { BaseAddress = new Uri(options.ApiUrl.TrimEnd('/')) };
        if (!_queryHttpClient.DefaultRequestHeaders.Contains("X-API-Key") && !string.IsNullOrEmpty(options.ApiKey))
            _queryHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", options.ApiKey);
        foreach (var integration in options.Integrations)
            integration.Setup(this);
    }

    public void Log(LogEntry entry)
    {
        if (Volatile.Read(ref _disposed) == 1) return;
        var scope = LogTideScope.Current;
        if (string.IsNullOrEmpty(entry.TraceId))
            entry.TraceId = scope?.TraceId ?? (_options.AutoTraceId ? W3CTraceContext.GenerateTraceId() : null);
        if (string.IsNullOrEmpty(entry.SpanId))
            entry.SpanId = scope?.SpanId;
        if (string.IsNullOrEmpty(entry.SessionId))
            entry.SessionId = scope?.SessionId;

        if (scope != null)
        {
            var crumbs = scope.GetBreadcrumbs();
            if (crumbs.Count > 0) entry.Metadata.TryAdd("breadcrumbs", crumbs);
        }
        foreach (var kvp in _options.GlobalMetadata)
            entry.Metadata.TryAdd(kvp.Key, kvp.Value);

        _transport.Enqueue(entry);
    }

    public void Debug(string service, string message, Dictionary<string, object?>? metadata = null)
        => Log(new LogEntry { Service = service, Level = LogLevel.Debug, Message = message, Metadata = metadata ?? new() });
    public void Info(string service, string message, Dictionary<string, object?>? metadata = null)
        => Log(new LogEntry { Service = service, Level = LogLevel.Info, Message = message, Metadata = metadata ?? new() });
    public void Warn(string service, string message, Dictionary<string, object?>? metadata = null)
        => Log(new LogEntry { Service = service, Level = LogLevel.Warn, Message = message, Metadata = metadata ?? new() });
    public void Error(string service, string message, Dictionary<string, object?>? metadata = null)
        => Log(new LogEntry { Service = service, Level = LogLevel.Error, Message = message, Metadata = metadata ?? new() });
    public void Error(string service, string message, Exception exception)
        => Log(new LogEntry { Service = service, Level = LogLevel.Error, Message = message, Metadata = new() { ["error"] = SerializeException(exception) } });
    public void Critical(string service, string message, Dictionary<string, object?>? metadata = null)
        => Log(new LogEntry { Service = service, Level = LogLevel.Critical, Message = message, Metadata = metadata ?? new() });
    public void Critical(string service, string message, Exception exception)
        => Log(new LogEntry { Service = service, Level = LogLevel.Critical, Message = message, Metadata = new() { ["error"] = SerializeException(exception) } });

    public Task FlushAsync(CancellationToken cancellationToken = default) => _transport.FlushAsync(cancellationToken);

    public Span StartSpan(string name, string? parentSpanId = null)
    {
        var traceId = LogTideScope.Current?.TraceId ?? W3CTraceContext.GenerateTraceId();
        var span = _spanManager.StartSpan(name, traceId, parentSpanId);
        if (LogTideScope.Current != null) LogTideScope.Current.SpanId = span.SpanId;
        return span;
    }

    public void FinishSpan(Span span, SpanStatus status = SpanStatus.Ok)
    {
        if (_spanManager.TryFinishSpan(span.SpanId, status, out var finished) && finished != null)
            _transport.EnqueueSpan(finished);
    }

    public void AddBreadcrumb(Breadcrumb breadcrumb)
        => LogTideScope.Current?.AddBreadcrumb(breadcrumb);

    public ClientMetrics GetMetrics() => _transport.GetMetrics();
    public void ResetMetrics() => _transport.ResetMetrics();
    public CircuitState GetCircuitBreakerState() => _transport.CircuitBreakerState;

    #region Query Methods

    public async Task<LogsResponse> QueryAsync(QueryOptions options, CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>();

        if (!string.IsNullOrEmpty(options.Service))
            queryParams.Add($"service={Uri.EscapeDataString(options.Service)}");
        if (options.Level.HasValue)
            queryParams.Add($"level={options.Level.Value.ToApiString()}");
        if (options.From.HasValue)
            queryParams.Add($"from={Uri.EscapeDataString(options.From.Value.ToString("O"))}");
        if (options.To.HasValue)
            queryParams.Add($"to={Uri.EscapeDataString(options.To.Value.ToString("O"))}");
        if (!string.IsNullOrEmpty(options.Query))
            queryParams.Add($"q={Uri.EscapeDataString(options.Query)}");
        queryParams.Add($"limit={options.Limit}");
        queryParams.Add($"offset={options.Offset}");

        var url = $"/api/v1/logs?{string.Join("&", queryParams)}";
        using var response = await _queryHttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new ApiException((int)response.StatusCode, errorBody);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<LogsResponse>(json, JsonConfig.Options);
        return result ?? new LogsResponse();
    }

    public async Task<List<LogEntry>> GetByTraceIdAsync(string traceId, CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/logs/trace/{Uri.EscapeDataString(traceId)}";
        using var response = await _queryHttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new ApiException((int)response.StatusCode, errorBody);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<LogsResponse>(json, JsonConfig.Options);
        return result?.Logs ?? new List<LogEntry>();
    }

    public async Task<AggregatedStatsResponse> GetAggregatedStatsAsync(
        AggregatedStatsOptions options,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"from={Uri.EscapeDataString(options.From.ToString("O"))}",
            $"to={Uri.EscapeDataString(options.To.ToString("O"))}",
            $"interval={Uri.EscapeDataString(options.Interval)}"
        };

        if (!string.IsNullOrEmpty(options.Service))
            queryParams.Add($"service={Uri.EscapeDataString(options.Service)}");

        var url = $"/api/v1/logs/aggregated?{string.Join("&", queryParams)}";
        using var response = await _queryHttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new ApiException((int)response.StatusCode, errorBody);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<AggregatedStatsResponse>(json, JsonConfig.Options);
        return result ?? new AggregatedStatsResponse();
    }

    #endregion

    private static Dictionary<string, object?> SerializeException(Exception ex)
    {
        var r = new Dictionary<string, object?> { ["type"] = ex.GetType().FullName, ["message"] = ex.Message, ["stack"] = ex.StackTrace };
        if (ex.InnerException != null) r["cause"] = SerializeException(ex.InnerException);
        return r;
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;
        foreach (var i in _options.Integrations) i.Teardown();
        _transport.Dispose();
        if (_ownsHttpClient) _queryHttpClient.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;
        foreach (var i in _options.Integrations) i.Teardown();
        await _transport.DisposeAsync().ConfigureAwait(false);
        if (_ownsHttpClient) _queryHttpClient.Dispose();
    }
}
