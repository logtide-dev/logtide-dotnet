using LogTide.SDK.Breadcrumbs;
using LogTide.SDK.Core;
using LogTide.SDK.Integrations;
using LogTide.SDK.Models;
using LogTide.SDK.Enums;
using LogTide.SDK.Tracing;

// Advanced usage example with all features

Console.WriteLine("LogTide SDK - Advanced Example");
Console.WriteLine("==============================\n");

// Create client with full configuration
var options = new ClientOptions
{
    ApiUrl = "http://localhost:8080",
    ApiKey = "lp_your_api_key_here",
    ServiceName = "advanced-example",

    // Batching
    BatchSize = 50,
    FlushIntervalMs = 3000,

    // Buffer management
    MaxBufferSize = 5000,

    // Retry logic
    MaxRetries = 3,
    RetryDelayMs = 500,

    // Circuit breaker
    CircuitBreakerThreshold = 3,
    CircuitBreakerResetMs = 10000,

    // Options
    EnableMetrics = true,
    Debug = true,

    // Integrations
    Integrations = [new GlobalErrorIntegration()],

    // Global metadata added to every log
    GlobalMetadata = new Dictionary<string, object?>
    {
        ["environment"] = "development",
        ["version"] = "2.0.0",
        ["machine"] = Environment.MachineName
    }
};

await using var client = LogTideClient.FromDsn("https://lp_key@api.logtide.dev", options);

// 1. AsyncLocal Scope-based tracing (replaces SetTraceId/WithTraceId)
Console.WriteLine("1. Scope-based Tracing");
using (var scope = LogTideScope.Create())
{
    client.Info("advanced", "Log with auto-generated W3C trace ID");
    Console.WriteLine($"  Trace ID: {scope.TraceId}");

    // 2. Span tracking
    Console.WriteLine("\n2. Span Tracking");
    var span = client.StartSpan("process-order");
    span.SetAttribute("order.id", "ORD-123");

    client.Info("advanced", "Processing order");
    await Task.Delay(50); // simulate work

    span.AddEvent("validation-complete");
    await Task.Delay(50);

    client.FinishSpan(span, SpanStatus.Ok);
    Console.WriteLine($"  Span: {span.Name} ({span.SpanId})");

    // 3. Breadcrumbs
    Console.WriteLine("\n3. Breadcrumbs");
    client.AddBreadcrumb(new Breadcrumb { Message = "User clicked button", Type = "ui" });
    client.AddBreadcrumb(new Breadcrumb { Message = "API call started", Type = "http" });
    client.Info("advanced", "Action with breadcrumbs");
    Console.WriteLine($"  Breadcrumbs: {scope.GetBreadcrumbs().Count}");
}

// 4. Custom log entries
Console.WriteLine("\n4. Custom Log Entries");
client.Log(new LogEntry
{
    Service = "custom-service",
    Level = LogLevel.Info,
    Message = "Custom log entry",
    Metadata = new Dictionary<string, object?>
    {
        ["custom"] = "data",
        ["nested"] = new Dictionary<string, object?> { ["key"] = "value" }
    }
});

// 5. Error serialization
Console.WriteLine("\n5. Error Serialization");
try
{
    try { throw new InvalidOperationException("Inner exception"); }
    catch (Exception inner) { throw new ApplicationException("Outer exception", inner); }
}
catch (Exception ex)
{
    client.Error("advanced", "Nested exception example", ex);
}

// 6. Metrics
Console.WriteLine("\n6. Metrics");
await client.FlushAsync();
var metrics = client.GetMetrics();
Console.WriteLine($"  Logs sent: {metrics.LogsSent}");
Console.WriteLine($"  Circuit state: {client.GetCircuitBreakerState()}");

Console.WriteLine("\nDone!");
