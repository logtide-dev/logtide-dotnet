# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.8.3] - 2026-03-23

### Added

- **W3C traceparent** distributed tracing (replaces `X-Trace-Id`)
- **AsyncLocal `LogTideScope`** for ambient trace context across async flows
- **Span tracking** with `StartSpan`/`FinishSpan` and OpenTelemetry-compatible OTLP export
- **Breadcrumbs** — `Breadcrumb` model and ring-buffer `BreadcrumbBuffer` attached to scopes
- **Composable transport layer** — `ILogTransport`, `ISpanTransport`, `BatchTransport`, `LogTideHttpTransport`, `OtlpHttpTransport`
- **`ILogTideClient` interface** for DI and testability (with `NSubstitute`)
- **Integration system** — `IIntegration` interface, `GlobalErrorIntegration` for unhandled exceptions
- **Serilog sink** — new `LogTide.SDK.Serilog` project with `LogTideSink` and `WriteTo.LogTide()` extension
- **DSN connection string** support via `ClientOptions.FromDsn()` and `LogTideClient.FromDsn()`
- `SpanId`, `SessionId` fields on `LogEntry`
- `ServiceName`, `TracesSampleRate`, `Integrations` on `ClientOptions`
- `LogTideErrorHandlerMiddleware` for catching unhandled exceptions in ASP.NET Core
- Sensitive header filtering in middleware (`Authorization`, `Cookie`, `X-API-Key`, etc.)
- `NuGetAudit` enabled on all projects
- `FinishSpan` added to `ILogTideClient` interface

### Changed

- Target frameworks: `net8.0;net9.0` (dropped net6.0, net7.0)
- `LangVersion` updated to 13
- `LogTideClient` rewritten as thin facade over `BatchTransport` with scope enrichment
- `LogTideClient` is now `sealed` and implements `ILogTideClient`
- Middleware now uses W3C `traceparent` header (fallback to `X-Trace-Id`)
- Middleware resolved from DI (`ILogTideClient`) instead of `LogTideMiddlewareOptions.Client`
- `AddLogTide()` now registers `IHttpClientFactory` named client
- `System.Text.Json` updated to 9.0.0, `Microsoft.Extensions.Http` to 9.0.0

### Fixed

- **Circuit breaker HalfOpen** now allows exactly one probe (was allowing unlimited)
- **`DisposeAsync`** was setting `_disposed=true` before flushing, silently dropping all buffered logs
- **`Dispose()`** was not flushing buffered logs at all
- **Double-counting `LogsDropped`** when circuit breaker rejected a batch
- **`RecordFailure` called per retry attempt** instead of once — circuit breaker tripped prematurely
- **`FromDsn` HttpClient** was never disposed (resource leak)
- **`IHttpClientFactory` constructor** created 3 separate `HttpClient` instances instead of 1
- **`W3CTraceContext.Parse`** now validates hex characters, flags field, and rejects all-zeros trace/span IDs per W3C spec
- **Duplicate `X-API-Key` header** when `FromDsn` passed pre-configured HttpClient to constructor
- **Span leak in middleware** when `LogRequest` threw before `try` block — now wrapped in `try/finally`
- **Dispose race condition** — non-atomic check-then-act on `_disposed` replaced with `Interlocked.CompareExchange`
- **Sync-over-async deadlock** — `Dispose()` now uses `Task.Run` to avoid `SynchronizationContext` capture
- **`GlobalErrorIntegration._client`** visibility across threads (now `volatile`)
- Removed vulnerable `System.Net.Http 4.3.4` and `System.Text.RegularExpressions 4.3.1` from test project
- Removed vulnerable `Microsoft.AspNetCore.Http.Abstractions 2.2.0` explicit pin (uses `FrameworkReference` now)
- Removed unnecessary `System.Text.Encodings.Web` explicit pin

### Removed

- `SetTraceId()`, `GetTraceId()`, `WithTraceId()`, `WithNewTraceId()` — use `LogTideScope.Create(traceId)`
- `LogTideMiddlewareOptions.Client` property — client resolved from DI
- `LogTideMiddlewareOptions.TraceIdHeader` property — W3C `traceparent` is now the standard
- `Moq` dependency — replaced with `NSubstitute`

## [0.1.0] - 2026-01-13

### Added

- Initial release of LogTide .NET SDK
- Automatic batching with configurable size and interval
- Retry logic with exponential backoff
- Circuit breaker pattern for fault tolerance
- Max buffer size with drop policy
- Query API for searching and filtering logs
- Aggregated statistics API
- Trace ID context for distributed tracing
- Global metadata support
- Structured error serialization
- Internal metrics tracking
- Logging methods: Debug, Info, Warn, Error, Critical
- Thread-safe operations
- ASP.NET Core middleware for auto-logging HTTP requests
- Dependency injection support
- Full async/await support
- Support for .NET 6.0, 7.0, and 8.0

[0.8.3]: https://github.com/logtide-dev/logtide-sdk-csharp/compare/v0.1.0...v0.8.3
[0.1.0]: https://github.com/logtide-dev/logtide-sdk-csharp/releases/tag/v0.1.0
