<p align="center">
  <img src="https://raw.githubusercontent.com/logtide-dev/logtide/main/docs/images/logo.png" alt="LogTide Logo" width="400">
</p>

<h1 align="center">LogTide .NET SDK</h1>

<p align="center">
  <a href="https://www.nuget.org/packages/LogTide.SDK"><img src="https://img.shields.io/nuget/v/LogTide.SDK?color=blue" alt="NuGet"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-blue.svg" alt="License"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-8.0+-purple.svg" alt=".NET"></a>
  <a href="https://github.com/logtide-dev/logtide-sdk-csharp/releases"><img src="https://img.shields.io/github/v/release/logtide-dev/logtide-sdk-csharp" alt="Release"></a>
</p>

<p align="center">
  Official .NET SDK for <a href="https://logtide.dev">LogTide</a> with automatic batching, retry logic, circuit breaker, W3C distributed tracing, span tracking, breadcrumbs, and ASP.NET Core middleware support.
</p>

---

## Features

- **Automatic batching** with configurable size and interval
- **Retry logic** with exponential backoff
- **Circuit breaker** pattern for fault tolerance
- **W3C traceparent** distributed tracing
- **Span tracking** with OpenTelemetry-compatible export
- **AsyncLocal scope** for ambient trace context
- **Breadcrumbs** for event tracking within a scope
- **Composable transport layer** (LogTide HTTP, OTLP)
- **Integration system** (global error handler, extensible)
- **Serilog sink** (`LogTide.SDK.Serilog`)
- **DSN connection string** support
- **ASP.NET Core middleware** with sensitive header filtering
- **Query API** for searching and filtering logs
- **Dependency injection** with `IHttpClientFactory`
- **Full async/await support**
- **Thread-safe**

## Requirements

- .NET 8.0 or .NET 9.0

## Installation

```bash
dotnet add package LogTide.SDK
```

For Serilog integration:

```bash
dotnet add package LogTide.SDK.Serilog
```

## Quick Start

```csharp
using LogTide.SDK.Core;
using LogTide.SDK.Models;

// Create client with DSN
await using var client = LogTideClient.FromDsn("https://lp_your_key@api.logtide.dev");

// Send logs
client.Info("api-gateway", "Server started", new() { ["port"] = 3000 });
client.Error("database", "Connection failed", new Exception("Timeout"));

// Use scoped tracing
using (var scope = LogTideScope.Create())
{
    client.Info("api", "Request received");   // automatically gets W3C trace ID
    client.Info("db", "Query executed");       // same trace ID
}
```

---

## ASP.NET Core Integration

```csharp
using LogTide.SDK.Core;
using LogTide.SDK.Middleware;
using LogTide.SDK.Models;

var builder = WebApplication.CreateBuilder(args);

// Register LogTide with IHttpClientFactory
builder.Services.AddLogTide(new ClientOptions
{
    ApiUrl = builder.Configuration["LogTide:ApiUrl"]!,
    ApiKey = builder.Configuration["LogTide:ApiKey"]!,
    ServiceName = "my-api",
    GlobalMetadata = new() { ["env"] = builder.Environment.EnvironmentName }
});

var app = builder.Build();

// Catch unhandled exceptions
app.UseLogTideErrors();

// Auto-log HTTP requests with W3C traceparent support
app.UseLogTide(o => o.ServiceName = "my-api");

app.MapGet("/", (ILogTideClient logger) =>
{
    logger.Info("my-api", "Hello!");
    return Results.Ok();
});

app.Run();
```

The middleware automatically:
- Parses incoming `traceparent` headers (W3C standard)
- Creates a `LogTideScope` per request
- Starts and finishes a span per request
- Emits `traceparent` response header
- Filters sensitive headers (`Authorization`, `Cookie`, etc.)

---

## Span Tracking

```csharp
using var scope = LogTideScope.Create();

var span = client.StartSpan("process-order");
span.SetAttribute("order.id", "ORD-123");

// ... do work ...

span.AddEvent("validation-complete");
client.FinishSpan(span, SpanStatus.Ok);
```

Spans are exported in OTLP format to `/v1/otlp/traces`.

---

## Breadcrumbs

```csharp
using var scope = LogTideScope.Create();

client.AddBreadcrumb(new Breadcrumb { Message = "User clicked button", Type = "ui" });
client.AddBreadcrumb(new Breadcrumb { Message = "API call started", Type = "http" });

// Breadcrumbs are automatically attached to logs within this scope
client.Error("app", "Something failed");
```

---

## Serilog Integration

```csharp
using LogTide.SDK.Serilog;

await using var logtideClient = LogTideClient.FromDsn("https://lp_key@api.logtide.dev");

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.LogTide(logtideClient, serviceName: "my-service")
    .CreateLogger();

Log.Information("User {UserId} logged in", 42);  // forwarded to LogTide
```

### Alternative: Serilog.Sinks.OpenTelemetry

If you prefer to use the community [`Serilog.Sinks.OpenTelemetry`](https://github.com/serilog/serilog-sinks-opentelemetry) package directly, for example to share a single sink configuration for logs and traces, point it at LogTide's OTLP endpoints:

```csharp
using Serilog;
using Serilog.Sinks.OpenTelemetry;

Log.Logger = new LoggerConfiguration()
    .WriteTo.OpenTelemetry(options =>
    {
        options.LogsEndpoint   = "https://your-logtide-host/v1/otlp/logs";
        options.TracesEndpoint = "https://your-logtide-host/v1/otlp/traces";
        options.Protocol = OtlpProtocol.HttpProtobuf;
        options.Headers = new Dictionary<string, string>
        {
            ["X-API-Key"] = "lp_your_key"
        };
    })
    .CreateLogger();
```

> **Important:** always set `LogsEndpoint` and `TracesEndpoint` to the **full URL including the path**. Setting only `options.Endpoint = "https://your-logtide-host"` causes `Serilog.Sinks.OpenTelemetry` to auto-append `/v1/logs` and `/v1/traces`, which do **not** match LogTide's OTLP routes (`/v1/otlp/logs` and `/v1/otlp/traces`). If you set `Endpoint` to the full OTLP path, the sink will append the suffix again and produce a broken URL like `.../v1/otlp/logs/v1/logs`.

---

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ApiUrl` | `string` | **required** | Base URL of your LogTide instance |
| `ApiKey` | `string` | **required** | Project API key (starts with `lp_`) |
| `Dsn` | `string?` | `null` | DSN string (alternative to ApiUrl + ApiKey) |
| `ServiceName` | `string` | `"app"` | Service name for tracing |
| `BatchSize` | `int` | `100` | Logs to batch before sending |
| `FlushIntervalMs` | `int` | `5000` | Auto-flush interval in ms |
| `MaxBufferSize` | `int` | `10000` | Max buffer size (drop policy) |
| `MaxRetries` | `int` | `3` | Retry attempts on failure |
| `RetryDelayMs` | `int` | `1000` | Initial retry delay (exponential backoff) |
| `CircuitBreakerThreshold` | `int` | `5` | Failures before opening circuit |
| `CircuitBreakerResetMs` | `int` | `30000` | Time before retrying after circuit opens |
| `TracesSampleRate` | `double` | `1.0` | Sample rate for traces |
| `Integrations` | `List<IIntegration>` | `[]` | Integrations to register |
| `GlobalMetadata` | `Dictionary` | `{}` | Metadata added to all logs |
| `Debug` | `bool` | `false` | Enable debug logging to console |

---

## Breaking Changes (v0.8.3)

- `SetTraceId()`, `GetTraceId()`, `WithTraceId()`, `WithNewTraceId()` removed — use `LogTideScope.Create(traceId)`
- `LogTideMiddlewareOptions.Client` removed — client resolved from DI
- Default trace header: `X-Trace-Id` replaced by W3C `traceparent`
- Target frameworks: `net8.0;net9.0` (dropped net6/net7)
- `LogTideClient` is now `sealed`, implements `ILogTideClient`
- `LogEntry` has new optional fields `SpanId`, `SessionId`

---

## Examples

See the [examples/](./examples) directory for complete working examples.

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

MIT License - see [LICENSE](LICENSE) for details.

## Links

- [LogTide Website](https://logtide.dev)
- [Documentation](https://logtide.dev/docs/sdks/dotnet/)
- [GitHub Issues](https://github.com/logtide-dev/logtide-sdk-csharp/issues)
