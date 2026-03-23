using System.Diagnostics;
using LogTide.SDK.Core;
using LogTide.SDK.Enums;
using LogTide.SDK.Models;
using LogTide.SDK.Tracing;
using Microsoft.AspNetCore.Http;

namespace LogTide.SDK.Middleware;

/// <summary>
/// Options for LogTide ASP.NET Core middleware.
/// </summary>
public class LogTideMiddlewareOptions
{
    /// <summary>
    /// Service name to use in logs.
    /// </summary>
    public string ServiceName { get; set; } = "aspnet-api";

    /// <summary>
    /// Whether to log incoming requests. Default: true.
    /// </summary>
    public bool LogRequests { get; set; } = true;

    /// <summary>
    /// Whether to log outgoing responses. Default: true.
    /// </summary>
    public bool LogResponses { get; set; } = true;

    /// <summary>
    /// Whether to log errors. Default: true.
    /// </summary>
    public bool LogErrors { get; set; } = true;

    /// <summary>
    /// Whether to include request headers in logs. Default: false.
    /// </summary>
    public bool IncludeHeaders { get; set; } = false;

    /// <summary>
    /// Whether to skip health check endpoints. Default: true.
    /// </summary>
    public bool SkipHealthCheck { get; set; } = true;

    /// <summary>
    /// Paths to skip logging for.
    /// </summary>
    public HashSet<string> SkipPaths { get; set; } = new();
}

/// <summary>
/// ASP.NET Core middleware for automatic HTTP request/response logging with W3C traceparent support.
/// </summary>
public class LogTideMiddleware
{
    private static readonly HashSet<string> SensitiveHeaders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "authorization", "cookie", "set-cookie",
            "x-api-key", "x-auth-token", "proxy-authorization"
        };

    private readonly RequestDelegate _next;
    private readonly LogTideMiddlewareOptions _options;
    private readonly ILogTideClient _client;

    public LogTideMiddleware(RequestDelegate next, LogTideMiddlewareOptions options, ILogTideClient client)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldSkip(context))
        {
            await _next(context);
            return;
        }

        // Parse or generate trace ID
        var traceId = GetOrGenerateTraceId(context);
        using var scope = LogTideScope.Create(traceId);

        // Start request span
        var span = _client.StartSpan($"{context.Request.Method} {context.Request.Path}");
        var spanStatus = SpanStatus.Ok;

        // Emit traceparent response header
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[W3CTraceContext.HeaderName] =
                W3CTraceContext.Create(scope.TraceId, span.SpanId);
            return Task.CompletedTask;
        });

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (_options.LogRequests)
                LogRequest(context);

            await _next(context);
            stopwatch.Stop();

            span.SetAttribute("http.status_code", context.Response.StatusCode);
            spanStatus = context.Response.StatusCode >= 500 ? SpanStatus.Error : SpanStatus.Ok;

            if (_options.LogResponses)
                LogResponse(context, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            spanStatus = SpanStatus.Error;

            span.AddEvent("exception", new Dictionary<string, object?>
            {
                ["message"] = ex.Message,
                ["type"] = ex.GetType().FullName
            });

            if (_options.LogErrors)
                LogError(context, ex, stopwatch.ElapsedMilliseconds);

            throw;
        }
        finally
        {
            _client.FinishSpan(span, spanStatus);
        }
    }

    private bool ShouldSkip(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (_options.SkipHealthCheck)
        {
            var lowerPath = path.ToLowerInvariant();
            if (lowerPath.Contains("/health") || lowerPath == "/ready" || lowerPath == "/live")
                return true;
        }

        return _options.SkipPaths.Contains(path);
    }

    private static string GetOrGenerateTraceId(HttpContext context)
    {
        // Try W3C traceparent first
        if (context.Request.Headers.TryGetValue(W3CTraceContext.HeaderName, out var traceparent))
        {
            var parsed = W3CTraceContext.Parse(traceparent!);
            if (parsed.HasValue) return parsed.Value.TraceId;
        }

        // Fallback to X-Trace-Id
        if (context.Request.Headers.TryGetValue("X-Trace-Id", out var legacyTraceId)
            && !string.IsNullOrEmpty(legacyTraceId))
        {
            return legacyTraceId!;
        }

        return W3CTraceContext.GenerateTraceId();
    }

    private void LogRequest(HttpContext context)
    {
        var request = context.Request;
        var metadata = new Dictionary<string, object?>
        {
            ["method"] = request.Method,
            ["path"] = request.Path.Value,
            ["query"] = request.QueryString.Value,
            ["user_agent"] = request.Headers["User-Agent"].ToString(),
            ["remote_ip"] = context.Connection.RemoteIpAddress?.ToString()
        };

        if (_options.IncludeHeaders)
        {
            metadata["headers"] = request.Headers
                .Where(h => !SensitiveHeaders.Contains(h.Key))
                .ToDictionary(h => h.Key, h => h.Value.ToString());
        }

        _client.Log(new LogEntry
        {
            Service = _options.ServiceName,
            Level = LogLevel.Info,
            Message = $"{request.Method} {request.Path}",
            Metadata = metadata
        });
    }

    private void LogResponse(HttpContext context, long durationMs)
    {
        var statusCode = context.Response.StatusCode;
        var level = statusCode >= 500 ? LogLevel.Error
            : statusCode >= 400 ? LogLevel.Warn
            : LogLevel.Info;

        _client.Log(new LogEntry
        {
            Service = _options.ServiceName,
            Level = level,
            Message = $"{context.Request.Method} {context.Request.Path} {statusCode} ({durationMs}ms)",
            Metadata = new Dictionary<string, object?>
            {
                ["method"] = context.Request.Method,
                ["path"] = context.Request.Path.Value,
                ["status_code"] = statusCode,
                ["duration_ms"] = durationMs
            }
        });
    }

    private void LogError(HttpContext context, Exception exception, long durationMs)
    {
        _client.Log(new LogEntry
        {
            Service = _options.ServiceName,
            Level = LogLevel.Error,
            Message = $"Request error: {exception.Message}",
            Metadata = new Dictionary<string, object?>
            {
                ["method"] = context.Request.Method,
                ["path"] = context.Request.Path.Value,
                ["duration_ms"] = durationMs,
                ["error"] = new Dictionary<string, object?>
                {
                    ["name"] = exception.GetType().Name,
                    ["message"] = exception.Message,
                    ["stack"] = exception.StackTrace
                }
            }
        });
    }
}
