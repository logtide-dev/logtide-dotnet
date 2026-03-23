using LogTide.SDK.Integrations;

namespace LogTide.SDK.Models;

/// <summary>
/// Configuration options for LogTideClient.
/// </summary>
public class ClientOptions
{
    /// <summary>
    /// Base URL of the LogTide API (e.g., "https://logtide.dev" or "http://localhost:8080").
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// Project API key (starts with "lp_").
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// DSN string that encodes both API URL and API key (e.g., "https://lp_mykey@api.logtide.dev").
    /// </summary>
    public string? Dsn { get; set; }

    /// <summary>
    /// Service name for tracing. Default: "app".
    /// </summary>
    public string ServiceName { get; set; } = "app";

    /// <summary>
    /// Sample rate for traces (0.0 to 1.0). Default: 1.0.
    /// </summary>
    public double TracesSampleRate { get; set; } = 1.0;

    /// <summary>
    /// Integrations to register on client initialization.
    /// </summary>
    public List<IIntegration> Integrations { get; set; } = [];

    /// <summary>
    /// Number of logs to batch before sending. Default: 100.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Interval in milliseconds to auto-flush logs. Default: 5000ms.
    /// </summary>
    public int FlushIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Maximum logs in buffer (prevents memory leak). Default: 10000.
    /// </summary>
    public int MaxBufferSize { get; set; } = 10000;

    /// <summary>
    /// Maximum retry attempts on failure. Default: 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial retry delay in milliseconds (uses exponential backoff). Default: 1000ms.
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Number of consecutive failures before opening circuit breaker. Default: 5.
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Time in milliseconds before retrying after circuit opens. Default: 30000ms.
    /// </summary>
    public int CircuitBreakerResetMs { get; set; } = 30000;

    /// <summary>
    /// Whether to track internal metrics. Default: true.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Enable debug logging to console. Default: false.
    /// </summary>
    public bool Debug { get; set; } = false;

    /// <summary>
    /// Global metadata added to all logs.
    /// </summary>
    public Dictionary<string, object?> GlobalMetadata { get; set; } = new();

    /// <summary>
    /// Automatically generate trace IDs for logs that don't have one. Default: false.
    /// </summary>
    public bool AutoTraceId { get; set; } = false;

    /// <summary>
    /// HTTP timeout in seconds. Default: 30.
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Creates ClientOptions from a DSN string.
    /// </summary>
    public static ClientOptions FromDsn(string dsn)
    {
        var uri = new Uri(dsn);
        return new ClientOptions
        {
            ApiUrl = $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? "" : $":{uri.Port}")}",
            ApiKey = uri.UserInfo
        };
    }

    /// <summary>
    /// Resolves DSN into ApiUrl and ApiKey if they are not already set.
    /// </summary>
    internal void Resolve()
    {
        if (string.IsNullOrEmpty(Dsn)) return;
        var parsed = FromDsn(Dsn);
        if (string.IsNullOrEmpty(ApiUrl)) ApiUrl = parsed.ApiUrl;
        if (string.IsNullOrEmpty(ApiKey)) ApiKey = parsed.ApiKey;
    }
}
