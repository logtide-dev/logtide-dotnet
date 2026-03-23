# LogTide .NET SDK — Full Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete refactor of LogTide .NET SDK: fix bugs + vulnerabilities, adopt W3C traceparent, AsyncLocal scope, composable transport, span tracking, integrations plugin system, Serilog sink, comprehensive tests.

**Architecture:** Single solution with two projects (`LogTide.SDK` + `LogTide.SDK.Serilog`). SDK reorganized into `Core/`, `Transport/`, `Tracing/`, `Integrations/`, `Breadcrumbs/` subfolders. `BatchTransport` owns buffering/retry/circuit-breaker; `LogTideClient` is a thin façade that enriches from `AsyncLocal<LogTideScope>` and delegates.

**Tech Stack:** .NET 8+9, xUnit 2.x, NSubstitute (replaces Moq), Serilog 4.x

---

## File Map

### Modified
- `LogTide.SDK.csproj` — TFMs net8+net9, remove vulnerable packages, update deps
- `LogTide.SDK.sln` — add Serilog project + test project
- `Models/LogEntry.cs` — add `SpanId`, `SessionId`
- `Models/ClientOptions.cs` — add `Dsn`, `ServiceName`, `TracesSampleRate`, `Integrations`
- `Internal/CircuitBreaker.cs` — fix HalfOpen probe (one probe at a time)
- `Middleware/LogTideMiddleware.cs` — W3C traceparent, scope, spans, sensitive header strip
- `Middleware/LogTideExtensions.cs` — IHttpClientFactory, new UseLogTideErrors()
- `tests/LogTide.SDK.Tests.csproj` — remove vulnerable packages, add NSubstitute
- `examples/*.cs`, `README.md`

### New (SDK)
- `Core/ILogTideClient.cs`
- `Core/LogTideClient.cs` (rewrite)
- `Core/LogTideScope.cs`
- `Transport/ITransport.cs`
- `Transport/LogTideHttpTransport.cs`
- `Transport/OtlpHttpTransport.cs`
- `Transport/BatchTransport.cs`
- `Tracing/Span.cs`
- `Tracing/SpanStatus.cs`
- `Tracing/SpanEvent.cs`
- `Tracing/SpanManager.cs`
- `Tracing/W3CTraceContext.cs`
- `Integrations/IIntegration.cs`
- `Integrations/GlobalErrorIntegration.cs`
- `Breadcrumbs/Breadcrumb.cs`
- `Breadcrumbs/BreadcrumbBuffer.cs`
- `Middleware/LogTideErrorHandlerMiddleware.cs`

### New (Serilog project)
- `Serilog/LogTide.SDK.Serilog.csproj`
- `Serilog/LogTideSink.cs`
- `Serilog/LogTideSinkExtensions.cs`

### New (Tests)
- `tests/LogTide.SDK.Tests/Helpers/FakeTransport.cs`
- `tests/LogTide.SDK.Tests/Helpers/FakeHttpMessageHandler.cs`
- `tests/LogTide.SDK.Tests/Core/LogTideScopeTests.cs`
- `tests/LogTide.SDK.Tests/Core/LogTideClientTests.cs` (rewrite)
- `tests/LogTide.SDK.Tests/Transport/BatchTransportTests.cs`
- `tests/LogTide.SDK.Tests/Tracing/W3CTraceContextTests.cs`
- `tests/LogTide.SDK.Tests/Tracing/SpanTests.cs`
- `tests/LogTide.SDK.Tests/Tracing/SpanManagerTests.cs`
- `tests/LogTide.SDK.Tests/Breadcrumbs/BreadcrumbBufferTests.cs`
- `tests/LogTide.SDK.Tests/Integrations/GlobalErrorIntegrationTests.cs`
- `tests/LogTide.SDK.Tests/Middleware/LogTideMiddlewareTests.cs`
- `tests/LogTide.SDK.Tests/Internal/CircuitBreakerTests.cs` (update)
- `tests/LogTide.SDK.Serilog.Tests/LogTide.SDK.Serilog.Tests.csproj`
- `tests/LogTide.SDK.Serilog.Tests/LogTideSinkTests.cs`

---

## Task 1: Fix vulnerabilities + update csproj

**Files:**
- Modify: `tests/LogTide.SDK.Tests.csproj`
- Modify: `LogTide.SDK.csproj`

- [ ] Remove `System.Net.Http 4.3.4` and `System.Text.RegularExpressions 4.3.1` from `tests/LogTide.SDK.Tests.csproj` (both are inbox on net8, explicit pins force vulnerable versions — CVE-2018-8292, CVE-2019-0820)
- [ ] Replace `Moq` with `NSubstitute` in test csproj (`<PackageReference Include="NSubstitute" Version="5.1.0" />`)
- [ ] Update `LogTide.SDK.csproj`: change `<TargetFrameworks>net6.0;net7.0;net8.0</TargetFrameworks>` → `net8.0;net9.0`
- [ ] Remove `Microsoft.AspNetCore.Http.Abstractions 2.2.0` explicit pin; replace with framework reference: `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (conditional on net8/net9)
- [ ] Remove `System.Text.Encodings.Web` explicit pin from SDK csproj — it is inbox on net8/net9; the explicit pin overrides the framework-supplied version unnecessarily
- [ ] Update `System.Text.Json` to `9.0.0`, `Microsoft.Extensions.Http` to `9.0.0`
- [ ] Set `<LangVersion>13</LangVersion>`
- [ ] Add `<NuGetAudit>true</NuGetAudit><NuGetAuditLevel>moderate</NuGetAuditLevel>` to SDK csproj
- [ ] Run `dotnet restore && dotnet build` — expect clean build
- [ ] Commit: `fix: remove vulnerable NuGet pins and drop EOL target frameworks`

---

## Task 2: Update models (LogEntry + ClientOptions)

**Files:**
- Modify: `Models/LogEntry.cs`
- Modify: `Models/ClientOptions.cs`
- Test: `tests/LogTide.SDK.Tests/Models/LogEntryTests.cs` (new)

- [ ] Write failing test: `LogEntry_HasSpanIdAndSessionId`
```csharp
[Fact]
public void LogEntry_DefaultsAreCorrect()
{
    var entry = new LogEntry();
    Assert.Null(entry.SpanId);
    Assert.Null(entry.SessionId);
}
```
- [ ] Add to `LogEntry.cs`:
```csharp
[JsonPropertyName("span_id")]
public string? SpanId { get; set; }

[JsonPropertyName("session_id")]
public string? SessionId { get; set; }
```
- [ ] Update `SerializableLogEntry` and its `FromLogEntry` to map `SpanId`, `SessionId`
- [ ] Write failing test: `ClientOptions_ParsesDsn`
```csharp
[Fact]
public void ClientOptions_ParsesDsn()
{
    var opts = ClientOptions.FromDsn("https://lp_mykey@api.logtide.dev");
    Assert.Equal("https://api.logtide.dev", opts.ApiUrl);
    Assert.Equal("lp_mykey", opts.ApiKey);
}
```
- [ ] Add to `ClientOptions.cs`:
```csharp
public string? Dsn { get; set; }
public string ServiceName { get; set; } = "app";
public double TracesSampleRate { get; set; } = 1.0;
public List<IIntegration> Integrations { get; set; } = [];

public static ClientOptions FromDsn(string dsn)
{
    var uri = new Uri(dsn);
    return new ClientOptions
    {
        ApiUrl = $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? "" : $":{uri.Port}")}",
        ApiKey = uri.UserInfo
    };
}

internal void Resolve()
{
    if (string.IsNullOrEmpty(Dsn)) return;
    var parsed = FromDsn(Dsn);
    if (string.IsNullOrEmpty(ApiUrl)) ApiUrl = parsed.ApiUrl;
    if (string.IsNullOrEmpty(ApiKey)) ApiKey = parsed.ApiKey;
}
```
- [ ] Fix stale XML comment (`logward.dev` → `logtide.dev`)
- [ ] Run tests: `dotnet test tests/LogTide.SDK.Tests/ -v` — expect pass
- [ ] Commit: `feat: add SpanId/SessionId to LogEntry, DSN support and Integrations to ClientOptions`

---

## Task 3: W3CTraceContext utility

**Files:**
- Create: `Tracing/W3CTraceContext.cs`
- Test: `tests/LogTide.SDK.Tests/Tracing/W3CTraceContextTests.cs`

- [ ] Write failing tests:
```csharp
public class W3CTraceContextTests
{
    [Fact]
    public void Parse_ValidTraceparent_ReturnsIds()
    {
        var result = W3CTraceContext.Parse("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
        Assert.NotNull(result);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", result.Value.TraceId);
        Assert.Equal("00f067aa0ba902b7", result.Value.SpanId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("00-short-00f067aa0ba902b7-01")]
    public void Parse_InvalidInput_ReturnsNull(string? input)
    {
        Assert.Null(W3CTraceContext.Parse(input));
    }

    [Fact]
    public void Create_ProducesValidFormat()
    {
        var header = W3CTraceContext.Create("4bf92f3577b34da6a3ce929d0e0e4736", "00f067aa0ba902b7");
        Assert.Matches(@"^00-[0-9a-f]{32}-[0-9a-f]{16}-0[01]$", header);
    }

    [Fact]
    public void GenerateTraceId_IsLowercaseHex32()
    {
        var id = W3CTraceContext.GenerateTraceId();
        Assert.Equal(32, id.Length);
        Assert.Matches("^[0-9a-f]{32}$", id);
    }

    [Fact]
    public void GenerateSpanId_IsLowercaseHex16()
    {
        var id = W3CTraceContext.GenerateSpanId();
        Assert.Equal(16, id.Length);
        Assert.Matches("^[0-9a-f]{16}$", id);
    }
}
```
- [ ] Implement `Tracing/W3CTraceContext.cs`:
```csharp
namespace LogTide.SDK.Tracing;

public static class W3CTraceContext
{
    public const string HeaderName = "traceparent";

    public static (string TraceId, string SpanId)? Parse(string? traceparent)
    {
        if (string.IsNullOrEmpty(traceparent)) return null;
        var parts = traceparent.Split('-');
        if (parts.Length != 4 || parts[0] != "00") return null;
        if (parts[1].Length != 32 || parts[2].Length != 16) return null;
        return (parts[1], parts[2]);
    }

    public static string Create(string traceId, string spanId, bool sampled = true) =>
        $"00-{traceId}-{spanId}-{(sampled ? "01" : "00")}";

    public static string GenerateTraceId() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    public static string GenerateSpanId() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
}
```
- [ ] Run tests — expect pass
- [ ] Commit: `feat: add W3CTraceContext utility`

---

## Task 4: Breadcrumbs

**Files:**
- Create: `Breadcrumbs/Breadcrumb.cs`
- Create: `Breadcrumbs/BreadcrumbBuffer.cs`
- Test: `tests/LogTide.SDK.Tests/Breadcrumbs/BreadcrumbBufferTests.cs`

- [ ] Write failing tests:
```csharp
public class BreadcrumbBufferTests
{
    [Fact]
    public void Add_StoresItems()
    {
        var buf = new BreadcrumbBuffer(maxSize: 5);
        buf.Add(new Breadcrumb { Message = "hello" });
        Assert.Single(buf.GetAll());
    }

    [Fact]
    public void Add_EvictsOldestWhenFull()
    {
        var buf = new BreadcrumbBuffer(maxSize: 3);
        buf.Add(new Breadcrumb { Message = "1" });
        buf.Add(new Breadcrumb { Message = "2" });
        buf.Add(new Breadcrumb { Message = "3" });
        buf.Add(new Breadcrumb { Message = "4" }); // should evict "1"
        var all = buf.GetAll();
        Assert.Equal(3, all.Count);
        Assert.Equal("2", all[0].Message);
        Assert.Equal("4", all[2].Message);
    }

    [Fact]
    public void GetAll_ReturnsSnapshot()
    {
        var buf = new BreadcrumbBuffer(2);
        buf.Add(new Breadcrumb { Message = "a" });
        var snap1 = buf.GetAll();
        buf.Add(new Breadcrumb { Message = "b" });
        Assert.Single(snap1); // snapshot unchanged
    }
}
```
- [ ] Implement `Breadcrumbs/Breadcrumb.cs`:
```csharp
namespace LogTide.SDK.Breadcrumbs;

public sealed class Breadcrumb
{
    public string Type { get; set; } = "custom";
    public string Message { get; set; } = string.Empty;
    public string? Level { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object?> Data { get; set; } = new();
}
```
- [ ] Implement `Breadcrumbs/BreadcrumbBuffer.cs`:
```csharp
namespace LogTide.SDK.Breadcrumbs;

internal sealed class BreadcrumbBuffer
{
    private readonly int _maxSize;
    private readonly Queue<Breadcrumb> _queue = new();
    private readonly object _lock = new();

    public BreadcrumbBuffer(int maxSize = 50) => _maxSize = maxSize;

    public void Add(Breadcrumb breadcrumb)
    {
        lock (_lock)
        {
            if (_queue.Count >= _maxSize) _queue.Dequeue();
            _queue.Enqueue(breadcrumb);
        }
    }

    public IReadOnlyList<Breadcrumb> GetAll()
    {
        lock (_lock) { return _queue.ToArray(); }
    }
}
```
- [ ] Run tests — expect pass
- [ ] Commit: `feat: add Breadcrumb and BreadcrumbBuffer`

---

## Task 5: Fix CircuitBreaker HalfOpen

**Files:**
- Modify: `Internal/CircuitBreaker.cs`
- Modify: `tests/LogTide.SDK.Tests/Internal/CircuitBreakerTests.cs`

- [ ] Write new failing test:
```csharp
[Fact]
public void HalfOpen_AllowsOnlyOneProbe()
{
    var cb = new CircuitBreaker(threshold: 1, resetTimeoutMs: 0);
    cb.RecordFailure(); // opens
    Thread.Sleep(1); // let reset timeout pass

    Assert.True(cb.CanAttempt());  // first probe allowed
    Assert.False(cb.CanAttempt()); // second blocked while probe in-flight
}

[Fact]
public void HalfOpen_FailedProbeReopens()
{
    var cb = new CircuitBreaker(threshold: 1, resetTimeoutMs: 0);
    cb.RecordFailure();
    Thread.Sleep(1);
    cb.CanAttempt(); // allow probe
    cb.RecordFailure(); // probe failed → reopen
    Assert.Equal(CircuitState.Open, cb.State);
}
```
- [ ] Update `Internal/CircuitBreaker.cs` — add `_halfOpenProbePending` flag:
```csharp
private bool _halfOpenProbePending;

public bool CanAttempt()
{
    lock (_lock)
    {
        UpdateState();
        if (_state == CircuitState.Closed) return true;
        if (_state == CircuitState.Open) return false;
        // HalfOpen: allow exactly one probe
        if (_halfOpenProbePending) return false;
        _halfOpenProbePending = true;
        return true;
    }
}

public void RecordSuccess()
{
    lock (_lock)
    {
        _failureCount = 0;
        _halfOpenProbePending = false;
        _state = CircuitState.Closed;
    }
}

public void RecordFailure()
{
    lock (_lock)
    {
        _failureCount++;
        _halfOpenProbePending = false;
        _lastFailureTime = DateTime.UtcNow;
        if (_failureCount >= _threshold) _state = CircuitState.Open;
    }
}
```
- [ ] Run all circuit breaker tests — expect pass
- [ ] Commit: `fix: circuit breaker HalfOpen now allows exactly one probe`

---

## Task 6: Span model + SpanManager

**Files:**
- Create: `Tracing/Span.cs` (includes SpanStatus, SpanEvent)
- Create: `Tracing/SpanManager.cs`
- Test: `tests/LogTide.SDK.Tests/Tracing/SpanTests.cs`
- Test: `tests/LogTide.SDK.Tests/Tracing/SpanManagerTests.cs`

- [ ] Write failing tests for Span:
```csharp
public class SpanTests
{
    [Fact]
    public void Span_InitialState_IsCorrect()
    {
        var span = new Span("abc123", "trace123", null, "HTTP GET");
        Assert.Equal("abc123", span.SpanId);
        Assert.Equal("trace123", span.TraceId);
        Assert.Equal("HTTP GET", span.Name);
        Assert.Equal(SpanStatus.Unset, span.Status);
        Assert.False(span.IsFinished);
    }

    [Fact]
    public void Finish_SetsEndTimeAndStatus()
    {
        var span = new Span("a", "b", null, "test");
        span.Finish(SpanStatus.Ok);
        Assert.True(span.IsFinished);
        Assert.Equal(SpanStatus.Ok, span.Status);
        Assert.NotNull(span.EndTime);
    }

    [Fact]
    public void SetAttribute_StoresValue()
    {
        var span = new Span("a", "b", null, "test");
        span.SetAttribute("http.method", "GET");
        Assert.Equal("GET", span.Attributes["http.method"]);
    }

    [Fact]
    public void AddEvent_AppendsToList()
    {
        var span = new Span("a", "b", null, "test");
        span.AddEvent("exception", new Dictionary<string, object?> { ["message"] = "oops" });
        Assert.Single(span.Events);
        Assert.Equal("exception", span.Events[0].Name);
    }
}
```
- [ ] Implement `Tracing/Span.cs`:
```csharp
namespace LogTide.SDK.Tracing;

public sealed class Span
{
    public string SpanId { get; }
    public string TraceId { get; }
    public string? ParentSpanId { get; }
    public string Name { get; set; }
    public DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndTime { get; private set; }
    public SpanStatus Status { get; private set; } = SpanStatus.Unset;
    public Dictionary<string, object?> Attributes { get; } = new();
    public List<SpanEvent> Events { get; } = new();
    public bool IsFinished => EndTime.HasValue;

    public Span(string spanId, string traceId, string? parentSpanId, string name)
    {
        SpanId = spanId; TraceId = traceId;
        ParentSpanId = parentSpanId; Name = name;
    }

    public void SetStatus(SpanStatus status) => Status = status;
    public void SetAttribute(string key, object? value) => Attributes[key] = value;
    public void AddEvent(string name, Dictionary<string, object?>? attrs = null)
        => Events.Add(new SpanEvent(name, DateTimeOffset.UtcNow, attrs ?? new()));
    public void Finish(SpanStatus status = SpanStatus.Ok)
    {
        Status = status;
        EndTime = DateTimeOffset.UtcNow;
    }
}

public sealed class SpanEvent
{
    public string Name { get; }
    public DateTimeOffset Timestamp { get; }
    public Dictionary<string, object?> Attributes { get; }
    public SpanEvent(string name, DateTimeOffset ts, Dictionary<string, object?> attrs)
    { Name = name; Timestamp = ts; Attributes = attrs; }
}

public enum SpanStatus { Unset, Ok, Error }
```
- [ ] Write failing tests for SpanManager:
```csharp
public class SpanManagerTests
{
    [Fact]
    public void StartSpan_ReturnsSpanWithCorrectFields()
    {
        var mgr = new SpanManager();
        var span = mgr.StartSpan("test", "trace123");
        Assert.Equal("trace123", span.TraceId);
        Assert.Equal("test", span.Name);
        Assert.NotEmpty(span.SpanId);
    }

    [Fact]
    public void FinishSpan_RemovesFromActive()
    {
        var mgr = new SpanManager();
        var span = mgr.StartSpan("test", "t");
        Assert.True(mgr.TryFinishSpan(span.SpanId, SpanStatus.Ok, out _));
        Assert.False(mgr.TryFinishSpan(span.SpanId, SpanStatus.Ok, out _));
    }

    [Fact]
    public void TryFinishSpan_UnknownId_ReturnsFalse()
    {
        var mgr = new SpanManager();
        Assert.False(mgr.TryFinishSpan("nonexistent", SpanStatus.Ok, out _));
    }
}
```
- [ ] Implement `Tracing/SpanManager.cs`:
```csharp
namespace LogTide.SDK.Tracing;

internal sealed class SpanManager
{
    private readonly ConcurrentDictionary<string, Span> _spans = new();

    public Span StartSpan(string name, string traceId, string? parentSpanId = null)
    {
        var span = new Span(W3CTraceContext.GenerateSpanId(), traceId, parentSpanId, name);
        _spans.TryAdd(span.SpanId, span);
        return span;
    }

    public bool TryFinishSpan(string spanId, SpanStatus status, out Span? span)
    {
        if (_spans.TryRemove(spanId, out span))
        { span.Finish(status); return true; }
        return false;
    }
}
```
- [ ] Run tests — expect pass
- [ ] Commit: `feat: add Span model, SpanEvent, SpanStatus, SpanManager`

---

## Task 7: ILogTideClient interface + LogTideScope

**Must come before Task 8** — `IIntegration.Setup(ILogTideClient)` and tests using `Substitute.For<ILogTideClient>()` require this interface to exist first.

**Files:**
- Create: `Core/ILogTideClient.cs`
- Create: `Core/LogTideScope.cs`
- Test: `tests/LogTide.SDK.Tests/Core/LogTideScopeTests.cs`

- [ ] Create `Core/ILogTideClient.cs`:
```csharp
namespace LogTide.SDK.Core;

public interface ILogTideClient : IDisposable, IAsyncDisposable
{
    void Log(LogEntry entry);
    void Debug(string service, string message, Dictionary<string, object?>? metadata = null);
    void Info(string service, string message, Dictionary<string, object?>? metadata = null);
    void Warn(string service, string message, Dictionary<string, object?>? metadata = null);
    void Error(string service, string message, Dictionary<string, object?>? metadata = null);
    void Error(string service, string message, Exception exception);
    void Critical(string service, string message, Dictionary<string, object?>? metadata = null);
    void Critical(string service, string message, Exception exception);
    Task FlushAsync(CancellationToken cancellationToken = default);
    Span StartSpan(string name, string? parentSpanId = null);
    void AddBreadcrumb(Breadcrumb breadcrumb);
    ClientMetrics GetMetrics();
    void ResetMetrics();
    CircuitState GetCircuitBreakerState();
}
```
- [ ] Write failing tests for LogTideScope:
```csharp
public class LogTideScopeTests
{
    [Fact]
    public void Create_SetsCurrentScope()
    {
        using var scope = LogTideScope.Create("abc123");
        Assert.Equal("abc123", LogTideScope.Current?.TraceId);
    }

    [Fact]
    public void Dispose_RestoresPreviousScope()
    {
        using var outer = LogTideScope.Create("outer");
        using (var inner = LogTideScope.Create("inner"))
        {
            Assert.Equal("inner", LogTideScope.Current?.TraceId);
        }
        Assert.Equal("outer", LogTideScope.Current?.TraceId);
    }

    [Fact]
    public void Create_WithNullTraceId_GeneratesId()
    {
        using var scope = LogTideScope.Create();
        Assert.NotNull(scope.TraceId);
        Assert.Equal(32, scope.TraceId.Length);
    }

    [Fact]
    public async Task AsyncLocal_IsolatesAcrossAsyncContexts()
    {
        string? traceInTask = null;
        using var scope = LogTideScope.Create("main-trace");

        await Task.Run(() =>
        {
            using var inner = LogTideScope.Create("task-trace");
            traceInTask = LogTideScope.Current?.TraceId;
        });

        // After task finishes, main context unchanged
        Assert.Equal("main-trace", LogTideScope.Current?.TraceId);
        Assert.Equal("task-trace", traceInTask);
    }

    [Fact]
    public void AddBreadcrumb_StoredInScope()
    {
        using var scope = LogTideScope.Create("t");
        scope.AddBreadcrumb(new Breadcrumb { Message = "click" });
        Assert.Single(scope.GetBreadcrumbs());
    }
}
```
- [ ] Implement `Core/LogTideScope.cs`:
```csharp
namespace LogTide.SDK.Core;

public sealed class LogTideScope : IDisposable
{
    private static readonly AsyncLocal<LogTideScope?> _current = new();

    public static LogTideScope? Current => _current.Value;

    public string TraceId { get; }
    public string? SpanId { get; internal set; }
    public string? SessionId { get; set; }

    private readonly BreadcrumbBuffer _breadcrumbs = new(maxSize: 50);
    private readonly LogTideScope? _previous;

    private LogTideScope(string traceId)
    {
        TraceId = traceId;
        _previous = _current.Value;
        _current.Value = this;
    }

    public static LogTideScope Create(string? traceId = null) =>
        new(traceId ?? W3CTraceContext.GenerateTraceId());

    public void AddBreadcrumb(Breadcrumb breadcrumb) => _breadcrumbs.Add(breadcrumb);
    public IReadOnlyList<Breadcrumb> GetBreadcrumbs() => _breadcrumbs.GetAll();

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _current.Value = _previous;
    }
}
```
- [ ] Add a concurrent-isolation test to `LogTideScopeTests`:
```csharp
[Fact]
public void Dispose_IsIdempotent()
{
    var scope = LogTideScope.Create("t");
    scope.Dispose();
    scope.Dispose(); // should not throw or corrupt state
}

[Fact]
public async Task ConcurrentRequests_HaveIsolatedScopes()
{
    string? trace1 = null, trace2 = null;
    var t1 = Task.Run(() => { using var s = LogTideScope.Create("req-1"); trace1 = LogTideScope.Current?.TraceId; });
    var t2 = Task.Run(() => { using var s = LogTideScope.Create("req-2"); trace2 = LogTideScope.Current?.TraceId; });
    await Task.WhenAll(t1, t2);
    Assert.Equal("req-1", trace1);
    Assert.Equal("req-2", trace2);
}
```
- [ ] Run tests — expect pass
- [ ] Commit: `feat: add ILogTideClient interface and AsyncLocal LogTideScope`

---

## Task 8: IIntegration + GlobalErrorIntegration

**Files:**
- Create: `Integrations/IIntegration.cs`
- Create: `Integrations/GlobalErrorIntegration.cs`
- Test: `tests/LogTide.SDK.Tests/Integrations/GlobalErrorIntegrationTests.cs`

- [ ] Create `Integrations/IIntegration.cs`:
```csharp
namespace LogTide.SDK.Integrations;

public interface IIntegration
{
    string Name { get; }
    void Setup(ILogTideClient client);
    void Teardown();
}
```
- [ ] Write failing test:
```csharp
public class GlobalErrorIntegrationTests
{
    [Fact]
    public void Setup_RegistersHandlers_TeardownUnregisters()
    {
        var client = Substitute.For<ILogTideClient>();
        var integration = new GlobalErrorIntegration();
        integration.Setup(client);
        Assert.Equal("GlobalError", integration.Name);
        integration.Teardown(); // should not throw
    }

    [Fact]
    public void OnUnobservedTaskException_CallsClientError()
    {
        var client = Substitute.For<ILogTideClient>();
        var integration = new GlobalErrorIntegration();
        integration.Setup(client);

        var ex = new AggregateException(new InvalidOperationException("oops"));
        integration.SimulateUnobservedTaskException(ex);

        client.Received(1).Error(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Exception>());
        integration.Teardown();
    }
}
```
- [ ] Implement `Integrations/GlobalErrorIntegration.cs` with internal test seam:
```csharp
namespace LogTide.SDK.Integrations;

public sealed class GlobalErrorIntegration : IIntegration
{
    private ILogTideClient? _client;
    public string Name => "GlobalError";

    public void Setup(ILogTideClient client)
    {
        _client = client;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public void Teardown()
    {
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            _client?.Critical("global", "Unhandled exception", ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _client?.Error("global", "Unobserved task exception", e.Exception);
        e.SetObserved();
    }

    internal void SimulateUnobservedTaskException(AggregateException ex)
        => OnUnobservedTaskException(null, new UnobservedTaskExceptionEventArgs(ex));
}
```
- [ ] Run tests — expect pass
- [ ] Commit: `feat: add IIntegration interface and GlobalErrorIntegration`

---

## Task 9: Transport layer

**Files:**
- Create: `Transport/ITransport.cs`
- Create: `Transport/LogTideHttpTransport.cs`
- Create: `Transport/OtlpHttpTransport.cs`
- Create: `Transport/BatchTransport.cs`
- Create: `tests/LogTide.SDK.Tests/Helpers/FakeTransport.cs`
- Test: `tests/LogTide.SDK.Tests/Transport/BatchTransportTests.cs`

- [ ] Create `Transport/ITransport.cs`:
```csharp
namespace LogTide.SDK.Transport;

internal interface ILogTransport
{
    Task SendAsync(IReadOnlyList<LogEntry> logs, CancellationToken ct = default);
}

internal interface ISpanTransport
{
    Task SendSpansAsync(IReadOnlyList<Span> spans, CancellationToken ct = default);
}
```
- [ ] Create `tests/LogTide.SDK.Tests/Helpers/FakeTransport.cs`:
```csharp
internal sealed class FakeTransport : ILogTransport, ISpanTransport
{
    public List<IReadOnlyList<LogEntry>> LogBatches { get; } = new();
    public List<IReadOnlyList<Span>> SpanBatches { get; } = new();
    public int CallCount => LogBatches.Count;
    public Exception? ThrowOn { get; set; }
    private int _failFirstN;

    /// <summary>Throw on the first N calls, then succeed.</summary>
    public void FailFirstN(int n) => _failFirstN = n;

    public Task SendAsync(IReadOnlyList<LogEntry> logs, CancellationToken ct = default)
    {
        if (_failFirstN > 0) { _failFirstN--; throw ThrowOn ?? new HttpRequestException("fake failure"); }
        if (ThrowOn != null) throw ThrowOn;
        LogBatches.Add(logs);
        return Task.CompletedTask;
    }

    public Task SendSpansAsync(IReadOnlyList<Span> spans, CancellationToken ct = default)
    {
        SpanBatches.Add(spans);
        return Task.CompletedTask;
    }
}
```
- [ ] Write failing BatchTransport tests:
```csharp
public class BatchTransportTests
{
    private static ClientOptions Opts(int batchSize = 100, int flushMs = 60000) => new()
    {
        ApiUrl = "http://localhost", ApiKey = "k",
        BatchSize = batchSize, FlushIntervalMs = flushMs,
        MaxRetries = 0, RetryDelayMs = 0
    };

    [Fact]
    public async Task Enqueue_TriggersBatchFlush_WhenBatchSizeReached()
    {
        var fake = new FakeTransport();
        await using var transport = new BatchTransport(fake, fake, Opts(batchSize: 2));

        var e1 = new LogEntry { Service = "s", Message = "1" };
        var e2 = new LogEntry { Service = "s", Message = "2" };
        transport.Enqueue(e1);
        transport.Enqueue(e2);
        await transport.FlushAsync();

        Assert.Single(fake.LogBatches);
        Assert.Equal(2, fake.LogBatches[0].Count);
    }

    [Fact]
    public async Task FlushAsync_EmptyBuffer_DoesNothing()
    {
        var fake = new FakeTransport();
        await using var transport = new BatchTransport(fake, fake, Opts());
        await transport.FlushAsync();
        Assert.Empty(fake.LogBatches);
    }

    [Fact]
    public async Task Enqueue_DropsLog_WhenBufferFull()
    {
        var fake = new FakeTransport();
        var opts = Opts(); opts.MaxBufferSize = 2;
        await using var transport = new BatchTransport(fake, fake, opts);

        transport.Enqueue(new LogEntry());
        transport.Enqueue(new LogEntry());
        Assert.Throws<BufferFullException>(() => transport.Enqueue(new LogEntry()));
    }

    [Fact]
    public async Task SendAsync_RetriesOnTransientFailure_ThenSucceeds()
    {
        var fake = new FakeTransport();
        fake.FailFirstN(2); // fail twice, succeed on third attempt
        var opts = Opts(); opts.MaxRetries = 3; opts.RetryDelayMs = 0;
        await using var transport = new BatchTransport(fake, fake, opts);

        transport.Enqueue(new LogEntry { Service = "svc", Message = "m" });
        await transport.FlushAsync();

        Assert.Single(fake.LogBatches); // one successful send on third attempt
        Assert.Equal(2, transport.GetMetrics().Retries);
    }

    [Fact]
    public async Task SendAsync_ExhaustsRetries_DropsLogs()
    {
        var fake = new FakeTransport();
        fake.FailFirstN(10); // always fail
        var opts = Opts(); opts.MaxRetries = 2; opts.RetryDelayMs = 0;
        await using var transport = new BatchTransport(fake, fake, opts);

        transport.Enqueue(new LogEntry { Service = "svc", Message = "m" });
        await transport.FlushAsync();

        Assert.Empty(fake.LogBatches);
        Assert.Equal(1, transport.GetMetrics().LogsDropped);
    }
}
```
- [ ] Implement `Transport/LogTideHttpTransport.cs` (extracted from old `SendLogsAsync`):
```csharp
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
        using var response = await _httpClient.PostAsync("/api/v1/ingest", content, ct);
        if (!response.IsSuccessStatusCode)
            throw new ApiException((int)response.StatusCode,
                await response.Content.ReadAsStringAsync(ct));
    }
}
```
- [ ] Implement `Transport/OtlpHttpTransport.cs`:
```csharp
namespace LogTide.SDK.Transport;

internal sealed class OtlpHttpTransport : ISpanTransport
{
    private readonly HttpClient _httpClient;
    private readonly string _serviceName;

    public OtlpHttpTransport(HttpClient httpClient, string serviceName)
    { _httpClient = httpClient; _serviceName = serviceName; }

    public async Task SendSpansAsync(IReadOnlyList<Span> spans, CancellationToken ct = default)
    {
        var payload = BuildOtlpPayload(spans);
        var json = JsonSerializer.Serialize(payload, JsonConfig.Options);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/otlp/traces", content, ct);
        if (!response.IsSuccessStatusCode)
            throw new ApiException((int)response.StatusCode,
                await response.Content.ReadAsStringAsync(ct));
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
        // SpanStatus enum: Unset=0, Ok=1, Error=2 — matches OTLP StatusCode exactly
        status = new { code = (int)s.Status },
        attributes = s.Attributes.Select(kv => new { key = kv.Key, value = new { stringValue = kv.Value?.ToString() } }).ToArray(),
        events = s.Events.Select(e => new { name = e.Name, timeUnixNano = e.Timestamp.ToUnixTimeMilliseconds() * 1_000_000L }).ToArray()
    };
}
```
- [ ] Implement `Transport/BatchTransport.cs` — moves buffering/retry/CB logic out of LogTideClient:
```csharp
namespace LogTide.SDK.Transport;

internal sealed class BatchTransport : IDisposable, IAsyncDisposable
{
    private readonly ILogTransport _logTransport;
    private readonly ISpanTransport? _spanTransport;
    private readonly ClientOptions _options;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly List<LogEntry> _logBuffer = new();
    private readonly List<Span> _spanBuffer = new();
    private readonly object _lock = new();
    private readonly object _metricsLock = new();
    private readonly Timer _flushTimer;
    private readonly List<double> _latencyWindow = new();
    private ClientMetrics _metrics = new();
    private bool _disposed;

    public BatchTransport(ILogTransport logTransport, ISpanTransport? spanTransport, ClientOptions options)
    {
        _logTransport = logTransport;
        _spanTransport = spanTransport;
        _options = options;
        _circuitBreaker = new CircuitBreaker(options.CircuitBreakerThreshold, options.CircuitBreakerResetMs);
        _flushTimer = new Timer(_ => FireAndForgetFlush(), null, options.FlushIntervalMs, options.FlushIntervalMs);
    }

    public void Enqueue(LogEntry entry)
    {
        bool shouldFlush;
        lock (_lock)
        {
            if (_logBuffer.Count >= _options.MaxBufferSize)
            {
                lock (_metricsLock) { _metrics.LogsDropped++; }
                throw new BufferFullException();
            }
            _logBuffer.Add(entry);
            shouldFlush = _logBuffer.Count >= _options.BatchSize;
        }
        // Fire flush AFTER releasing lock to avoid lock contention
        if (shouldFlush) FireAndForgetFlush();
    }

    public void EnqueueSpan(Span span)
    {
        lock (_lock) { _spanBuffer.Add(span); }
    }

    private void FireAndForgetFlush()
    {
        Task.Run(FlushAsync).ContinueWith(t =>
        {
            if (t.IsFaulted && _options.Debug)
                Console.WriteLine($"[LogTide] Flush error: {t.Exception?.GetBaseException().Message}");
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (_disposed) return;
        List<LogEntry> logs;
        List<Span> spans;
        lock (_lock)
        {
            if (_logBuffer.Count == 0 && _spanBuffer.Count == 0) return;
            logs = new List<LogEntry>(_logBuffer);
            spans = new List<Span>(_spanBuffer);
            _logBuffer.Clear();
            _spanBuffer.Clear();
        }

        if (logs.Count > 0) await SendWithRetryAsync(logs, ct);
        if (spans.Count > 0 && _spanTransport != null)
            await SendSpansWithRetryAsync(spans, ct);
    }

    private async Task SendWithRetryAsync(List<LogEntry> logs, CancellationToken ct)
    {
        var attempt = 0;
        var delay = _options.RetryDelayMs;
        while (attempt <= _options.MaxRetries)
        {
            try
            {
                if (!_circuitBreaker.CanAttempt())
                {
                    lock (_metricsLock) { _metrics.LogsDropped += logs.Count; _metrics.CircuitBreakerTrips++; }
                    throw new CircuitBreakerOpenException();
                }
                var sw = Stopwatch.StartNew();
                await _logTransport.SendAsync(logs, ct).ConfigureAwait(false);
                sw.Stop();
                _circuitBreaker.RecordSuccess();
                UpdateLatency(sw.Elapsed.TotalMilliseconds);
                lock (_metricsLock) { _metrics.LogsSent += logs.Count; }
                return;
            }
            catch (CircuitBreakerOpenException) { break; }
            catch (Exception ex)
            {
                attempt++;
                _circuitBreaker.RecordFailure();
                lock (_metricsLock) { _metrics.Errors++; if (attempt <= _options.MaxRetries) _metrics.Retries++; }
                if (attempt > _options.MaxRetries) break;
                await Task.Delay(delay, ct).ConfigureAwait(false);
                delay *= 2;
            }
        }
        lock (_metricsLock) { _metrics.LogsDropped += logs.Count; }
    }

    private async Task SendSpansWithRetryAsync(List<Span> spans, CancellationToken ct)
    {
        // Always catch — never silently lose exceptions in production
        try { await _spanTransport!.SendSpansAsync(spans, ct).ConfigureAwait(false); }
        catch (Exception ex)
        {
            if (_options.Debug)
                Console.WriteLine($"[LogTide] Span send error: {ex.Message}");
            // future: increment span drop metric here
        }
    }

    private void UpdateLatency(double ms)
    {
        lock (_metricsLock)
        {
            _latencyWindow.Add(ms);
            if (_latencyWindow.Count > 100) _latencyWindow.RemoveAt(0);
            _metrics.AvgLatencyMs = _latencyWindow.Average();
        }
    }

    public ClientMetrics GetMetrics() { lock (_metricsLock) { return _metrics.Clone(); } }
    public void ResetMetrics() { lock (_metricsLock) { _metrics = new(); _latencyWindow.Clear(); } }
    public CircuitState CircuitBreakerState => _circuitBreaker.State;

    // Sync Dispose: stop timer but do NOT block on network flush.
    // Callers should prefer `await using` to ensure logs are flushed.
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _flushTimer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _flushTimer.DisposeAsync().ConfigureAwait(false);
        await FlushAsync().ConfigureAwait(false);
    }
}
```
- [ ] Run tests — expect pass
- [ ] Commit: `feat: add composable transport layer (ILogTransport, ISpanTransport, BatchTransport, OtlpHttpTransport)`

---

## Task 10: Rewrite LogTideClient

**Files:**
- Rewrite: `Core/LogTideClient.cs` (replaces root `LogTideClient.cs`)
- Delete: root `LogTideClient.cs`
- Modify: `LogTide.SDK.csproj` — update `<RootNamespace>`
- Test: `tests/LogTide.SDK.Tests/Core/LogTideClientTests.cs`

- [ ] Write failing tests:
```csharp
public class LogTideClientTests
{
    private static (LogTideClient client, FakeTransport transport) Create(Action<ClientOptions>? configure = null)
    {
        var opts = new ClientOptions { ApiUrl = "http://localhost", ApiKey = "k", FlushIntervalMs = 60000 };
        configure?.Invoke(opts);
        var fake = new FakeTransport();
        var client = new LogTideClient(opts, fake, fake);
        return (client, fake);
    }

    [Fact]
    public void Log_EnrichesFromAmbientScope()
    {
        var (client, fake) = Create();
        using var scope = LogTideScope.Create("trace-abc");
        LogEntry? captured = null;
        // Use FakeTransport interception or expose internal buffer
        client.Info("svc", "hello");
        // After flush, log should have TraceId = "trace-abc"
        client.FlushAsync().Wait();
        Assert.Single(fake.LogBatches);
        Assert.Equal("trace-abc", fake.LogBatches[0][0].TraceId);
    }

    [Fact]
    public void Log_MergesGlobalMetadata()
    {
        var (client, fake) = Create(o => o.GlobalMetadata = new() { ["env"] = "test" });
        client.Info("svc", "msg");
        client.FlushAsync().Wait();
        Assert.Equal("test", fake.LogBatches[0][0].Metadata["env"]);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new LogTideClient(null!, new FakeTransport(), null));
    }

    [Fact]
    public void StartSpan_ReturnSpanWithAmbientTraceId()
    {
        var (client, _) = Create();
        using var scope = LogTideScope.Create("my-trace");
        var span = client.StartSpan("HTTP GET /test");
        Assert.Equal("my-trace", span.TraceId);
        Assert.Equal("HTTP GET /test", span.Name);
    }

    [Fact]
    public void AddBreadcrumb_StoredInCurrentScope()
    {
        var (client, _) = Create();
        using var scope = LogTideScope.Create("t");
        client.AddBreadcrumb(new Breadcrumb { Message = "btn click" });
        Assert.Single(scope.GetBreadcrumbs());
    }

    [Fact]
    public void GetMetrics_ReturnsClone()
    {
        var (client, _) = Create();
        var m1 = client.GetMetrics();
        var m2 = client.GetMetrics();
        Assert.NotSame(m1, m2);
    }
}
```
- [ ] Implement `Core/LogTideClient.cs`:
```csharp
namespace LogTide.SDK.Core;

public sealed class LogTideClient : ILogTideClient
{
    private readonly ClientOptions _options;
    private readonly BatchTransport _transport;
    private readonly SpanManager _spanManager = new();
    private readonly HttpClient _queryHttpClient; // dedicated client for query-only reads
    private bool _disposed;

    // For DI with IHttpClientFactory
    public LogTideClient(ClientOptions options, IHttpClientFactory httpClientFactory)
        : this(options,
            new LogTideHttpTransport(httpClientFactory.CreateClient("LogTide")),
            new OtlpHttpTransport(httpClientFactory.CreateClient("LogTide"), options.ServiceName),
            httpClientFactory.CreateClient("LogTide"))
    { }

    // For testing / direct construction
    internal LogTideClient(ClientOptions options, ILogTransport logTransport, ISpanTransport? spanTransport,
        HttpClient? queryHttpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Resolve(); // parse DSN if provided
        _transport = new BatchTransport(logTransport, spanTransport, options);
        // _queryHttpClient used for QueryAsync, GetByTraceIdAsync, GetAggregatedStatsAsync
        _queryHttpClient = queryHttpClient ?? new HttpClient { BaseAddress = new Uri(options.ApiUrl.TrimEnd('/')) };
        _queryHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", options.ApiKey);
        foreach (var integration in options.Integrations)
            integration.Setup(this);
    }

    public void Log(LogEntry entry)
    {
        if (_disposed) return;
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

    public Task FlushAsync(CancellationToken ct = default) => _transport.FlushAsync(ct);

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

    // Query methods: copy QueryAsync, GetByTraceIdAsync, GetAggregatedStatsAsync from old LogTideClient.cs
    // Replace all _httpClient references with _queryHttpClient
    // Add .ConfigureAwait(false) to all awaits in these methods

    private static Dictionary<string, object?> SerializeException(Exception ex)
    {
        var r = new Dictionary<string, object?> { ["type"] = ex.GetType().FullName, ["message"] = ex.Message, ["stack"] = ex.StackTrace };
        if (ex.InnerException != null) r["cause"] = SerializeException(ex.InnerException);
        return r;
    }

    // Sync Dispose: stops the timer but does NOT flush (would deadlock in sync contexts).
    // Always prefer `await using` in production to guarantee log delivery.
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var i in _options.Integrations) i.Teardown();
        _transport.Dispose(); // stops timer only, no network call
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var i in _options.Integrations) i.Teardown();
        await _transport.DisposeAsync().ConfigureAwait(false);
    }
}
```
- [ ] Copy `QueryAsync`, `GetByTraceIdAsync`, `GetAggregatedStatsAsync` verbatim from the old `LogTideClient.cs`, replacing all `_httpClient` references with `_queryHttpClient` and adding `.ConfigureAwait(false)` to all `await` calls
- [ ] Delete old `LogTideClient.cs` from root
- [ ] Run tests — expect pass
- [ ] Commit: `feat: rewrite LogTideClient as thin façade over BatchTransport with AsyncLocal scope enrichment`

---

## Task 11: Update middleware

**Files:**
- Modify: `Middleware/LogTideMiddleware.cs`
- Modify: `Middleware/LogTideExtensions.cs`
- Create: `Middleware/LogTideErrorHandlerMiddleware.cs`
- Test: `tests/LogTide.SDK.Tests/Middleware/LogTideMiddlewareTests.cs`

- [ ] Write failing middleware tests:
```csharp
public class LogTideMiddlewareTests
{
    private static readonly HashSet<string> SensitiveHeaders =
        new(StringComparer.OrdinalIgnoreCase) { "authorization", "cookie", "x-api-key" };

    [Fact]
    public async Task Middleware_SetsTraceIdFromTraceparent()
    {
        var fake = new FakeTransport();
        var opts = new ClientOptions { ApiUrl = "http://localhost", ApiKey = "k", FlushIntervalMs = 60000 };
        var client = new LogTideClient(opts, fake, null);

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

        var middleware = new LogTideMiddleware(
            _ => Task.CompletedTask,
            new LogTideMiddlewareOptions { ServiceName = "test" },
            client);

        await middleware.InvokeAsync(ctx);
        await client.FlushAsync();

        var loggedTraceId = fake.LogBatches.SelectMany(b => b).First().TraceId;
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", loggedTraceId);
    }

    [Fact]
    public async Task Middleware_FiltersSensitiveHeaders()
    {
        var fake = new FakeTransport();
        var opts = new ClientOptions { ApiUrl = "http://localhost", ApiKey = "k", FlushIntervalMs = 60000 };
        var client = new LogTideClient(opts, fake, null);

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "Bearer secret";
        ctx.Request.Headers["X-Custom"] = "safe";

        var middlewareOpts = new LogTideMiddlewareOptions { ServiceName = "test", IncludeHeaders = true };
        var middleware = new LogTideMiddleware(_ => Task.CompletedTask, middlewareOpts, client);

        await middleware.InvokeAsync(ctx);
        await client.FlushAsync();

        var headers = fake.LogBatches.SelectMany(b => b)
            .First().Metadata["headers"] as Dictionary<string, string>;
        Assert.DoesNotContain("Authorization", headers!.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("X-Custom", headers.Keys);
    }
}
```
- [ ] Update `Middleware/LogTideMiddleware.cs`:
  - Add static `SensitiveHeaders` set: `{ "authorization", "cookie", "set-cookie", "x-api-key", "x-auth-token", "proxy-authorization" }`
  - Replace `GetOrGenerateTraceId` to parse `traceparent` via `W3CTraceContext.Parse()`, fall back to `X-Trace-Id`, fall back to generate
  - In `InvokeAsync`: create `LogTideScope` with the traceId, call `client.StartSpan(...)`, finish span on response
  - Emit `traceparent` response header via `W3CTraceContext.Create()`
  - In `LogRequest`: when `IncludeHeaders`, filter with `SensitiveHeaders`
  - Middleware constructor: take `ILogTideClient` from DI (not from options)

- [ ] Update `Middleware/LogTideExtensions.cs`:
  - `AddLogTide(IServiceCollection, ClientOptions)` → register `IHttpClientFactory` named client, register `ILogTideClient` as singleton
  - `AddLogTide(IServiceCollection, Action<ClientOptions>)` overload
  - `UseLogTideErrors()` extension that adds `LogTideErrorHandlerMiddleware`

- [ ] Create `Middleware/LogTideErrorHandlerMiddleware.cs`:
```csharp
public class LogTideErrorHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogTideClient _client;
    private readonly string _serviceName;

    public LogTideErrorHandlerMiddleware(RequestDelegate next, ILogTideClient client, string serviceName = "aspnet-api")
    { _next = next; _client = client; _serviceName = serviceName; }

    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (Exception ex)
        {
            _client.Error(_serviceName, $"Unhandled exception: {ex.Message}", ex);
            throw;
        }
    }
}
```
- [ ] Run tests — expect pass
- [ ] Commit: `feat: update middleware with W3C traceparent, scope, spans, sensitive header filtering, error handler middleware`

---

## Task 12: Serilog sink project

**Files:**
- Create: `Serilog/LogTide.SDK.Serilog.csproj`
- Create: `Serilog/LogTideSink.cs`
- Create: `Serilog/LogTideSinkExtensions.cs`
- Modify: `LogTide.SDK.sln`
- Create: `tests/LogTide.SDK.Serilog.Tests/LogTide.SDK.Serilog.Tests.csproj`
- Create: `tests/LogTide.SDK.Serilog.Tests/LogTideSinkTests.cs`

- [ ] Create `Serilog/LogTide.SDK.Serilog.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>13</LangVersion>
    <Version>0.2.0</Version>
    <PackageId>LogTide.SDK.Serilog</PackageId>
    <NuGetAudit>true</NuGetAudit>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\LogTide.SDK.csproj" />
    <PackageReference Include="Serilog" Version="4.2.0" />
  </ItemGroup>
</Project>
```
- [ ] Create `tests/LogTide.SDK.Serilog.Tests/LogTide.SDK.Serilog.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>13</LangVersion>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="NSubstitute" Version="5.1.0" />
    <PackageReference Include="Serilog" Version="4.2.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Serilog\LogTide.SDK.Serilog.csproj" />
  </ItemGroup>
</Project>
```
- [ ] Write failing tests:
```csharp
public class LogTideSinkTests
{
    [Fact]
    public void Emit_MapsLevelsCorrectly()
    {
        var client = Substitute.For<ILogTideClient>();
        var sink = new LogTideSink(client, "test-svc");

        var levels = new[]
        {
            (LogEventLevel.Verbose, LogLevel.Debug),
            (LogEventLevel.Debug, LogLevel.Debug),
            (LogEventLevel.Information, LogLevel.Info),
            (LogEventLevel.Warning, LogLevel.Warn),
            (LogEventLevel.Error, LogLevel.Error),
            (LogEventLevel.Fatal, LogLevel.Critical),
        };

        foreach (var (serilogLevel, expectedLevel) in levels)
        {
            var evt = new LogEvent(DateTimeOffset.UtcNow, serilogLevel, null,
                MessageTemplate.Empty, Array.Empty<LogEventProperty>());
            sink.Emit(evt);
        }

        client.Received(6).Log(Arg.Any<LogEntry>());
    }

    [Fact]
    public void Emit_IncludesExceptionInMetadata()
    {
        LogEntry? captured = null;
        var client = Substitute.For<ILogTideClient>();
        client.When(c => c.Log(Arg.Any<LogEntry>()))
              .Do(ci => captured = ci.Arg<LogEntry>());

        var sink = new LogTideSink(client, "svc");
        var ex = new InvalidOperationException("boom");
        var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, ex,
            MessageTemplate.Empty, Array.Empty<LogEventProperty>());

        sink.Emit(evt);

        Assert.NotNull(captured);
        Assert.True(captured!.Metadata.ContainsKey("error"));
    }

    [Fact]
    public void Emit_MapsStructuredPropertiesToMetadata()
    {
        LogEntry? captured = null;
        var client = Substitute.For<ILogTideClient>();
        client.When(c => c.Log(Arg.Any<LogEntry>()))
              .Do(ci => captured = ci.Arg<LogEntry>());

        var sink = new LogTideSink(client, "svc");
        var props = new[] { new LogEventProperty("UserId", new ScalarValue(42)) };
        var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            new MessageTemplate("Hello {UserId}", Array.Empty<MessageTemplateToken>()), props);

        sink.Emit(evt);

        Assert.Equal(42, captured!.Metadata["UserId"]);
    }
}
```
- [ ] Implement `Serilog/LogTideSink.cs`:
```csharp
using Serilog.Core;
using Serilog.Events;
using LogTide.SDK.Core;
using LogTide.SDK.Enums;
using LogTide.SDK.Models;
using SerilogLogLevel = Serilog.Events.LogEventLevel;

namespace LogTide.SDK.Serilog;

public sealed class LogTideSink : ILogEventSink
{
    private readonly ILogTideClient _client;
    private readonly string _serviceName;

    public LogTideSink(ILogTideClient client, string serviceName = "app")
    { _client = client; _serviceName = serviceName; }

    public void Emit(LogEvent logEvent)
    {
        var metadata = new Dictionary<string, object?>();
        foreach (var p in logEvent.Properties)
            metadata[p.Key] = ExtractValue(p.Value);
        if (logEvent.Exception != null)
            metadata["error"] = SerializeException(logEvent.Exception);

        _client.Log(new LogEntry
        {
            Service = _serviceName,
            Level = MapLevel(logEvent.Level),
            Message = logEvent.RenderMessage(),
            Metadata = metadata
        });
    }

    private static LogLevel MapLevel(SerilogLogLevel level) => level switch
    {
        SerilogLogLevel.Verbose or SerilogLogLevel.Debug => LogLevel.Debug,
        SerilogLogLevel.Information => LogLevel.Info,
        SerilogLogLevel.Warning => LogLevel.Warn,
        SerilogLogLevel.Error => LogLevel.Error,
        SerilogLogLevel.Fatal => LogLevel.Critical,
        _ => LogLevel.Info
    };

    private static object? ExtractValue(LogEventPropertyValue value) => value switch
    {
        ScalarValue sv => sv.Value,
        SequenceValue seq => seq.Elements.Select(ExtractValue).ToArray(),
        StructureValue struc => struc.Properties.ToDictionary(p => p.Name, p => ExtractValue(p.Value) as object),
        DictionaryValue dict => dict.Elements.ToDictionary(
            kv => ExtractValue(kv.Key)?.ToString() ?? string.Empty,
            kv => ExtractValue(kv.Value)),
        _ => value.ToString()
    };

    private static Dictionary<string, object?> SerializeException(Exception ex)
    {
        var r = new Dictionary<string, object?> { ["type"] = ex.GetType().FullName, ["message"] = ex.Message, ["stack"] = ex.StackTrace };
        if (ex.InnerException != null) r["cause"] = SerializeException(ex.InnerException);
        return r;
    }
}
```
- [ ] Implement `Serilog/LogTideSinkExtensions.cs`:
```csharp
using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using LogTide.SDK.Core;

namespace LogTide.SDK.Serilog;

public static class LogTideSinkExtensions
{
    public static LoggerConfiguration LogTide(
        this LoggerSinkConfiguration sinkConfiguration,
        ILogTideClient client,
        string serviceName = "app",
        LogEventLevel minimumLevel = LogEventLevel.Verbose) =>
        sinkConfiguration.Sink(new LogTideSink(client, serviceName), minimumLevel);
}
```
- [ ] Add `Serilog/LogTide.SDK.Serilog.csproj` to solution: `dotnet sln add Serilog/LogTide.SDK.Serilog.csproj`
- [ ] Add test project to solution: `dotnet sln add tests/LogTide.SDK.Serilog.Tests/`
- [ ] Run Serilog sink tests — expect pass
- [ ] Commit: `feat: add Serilog sink project LogTide.SDK.Serilog`

---

## Task 13: Update examples + README

**Files:**
- Modify: `examples/BasicExample.cs`
- Modify: `examples/AspNetCoreExample.cs`
- Modify: `examples/AdvancedExample.cs`
- Create: `examples/SerilogExample.cs`
- Modify: `README.md`

- [ ] Update `BasicExample.cs` — use `ClientOptions.FromDsn()`, `LogTideScope.Create()`, W3C trace IDs
- [ ] Update `AspNetCoreExample.cs` — show `AddLogTide()` with `IHttpClientFactory`, `UseLogTide()`, `UseLogTideErrors()`
- [ ] Update `AdvancedExample.cs` — show span tracking, integrations, breadcrumbs
- [ ] Create `SerilogExample.cs` — minimal `WriteTo.LogTide(client)` setup
- [ ] Update `README.md`:
  - Update installation section (new TFMs, NuGet package name)
  - Quick start with DSN
  - ASP.NET Core setup
  - Serilog integration
  - W3C traceparent note
  - Span tracking
  - Remove all `SetTraceId`/`WithTraceId` references (breaking change note)
- [ ] Commit: `docs: update examples and README for v0.2 refactor`

---

## Task 14: Final integration test pass

- [ ] Run full test suite: `dotnet test --verbosity normal`
- [ ] Verify `dotnet build` clean with no warnings on both TFMs: `dotnet build -f net8.0 && dotnet build -f net9.0`
- [ ] Run `dotnet list package --vulnerable` — expect no vulnerabilities
- [ ] Fix any failing tests
- [ ] Commit: `test: full integration pass, all tests green`

---

## Breaking Changes Summary (for CHANGELOG)

- `SetTraceId()`, `GetTraceId()`, `WithTraceId()`, `WithNewTraceId()` → removed; use `LogTideScope.Create(traceId)`
- `LogTideMiddlewareOptions.Client` → removed; client resolved from DI
- Default trace header changed: `X-Trace-Id` → W3C `traceparent` (configurable fallback)
- `LangVersion` now 13, `TargetFrameworks` now `net8.0;net9.0`
- `LogTideClient` now `sealed`, implements `ILogTideClient`
- `LogEntry` has new optional fields `SpanId`, `SessionId`
