using LogTide.SDK.Core;
using LogTide.SDK.Models;

// Basic usage example

Console.WriteLine("LogTide SDK - Basic Example");
Console.WriteLine("===========================\n");

// Create client using DSN
await using var client = LogTideClient.FromDsn("https://lp_your_api_key@api.logtide.dev");

// Or create with explicit options:
// await using var client = new LogTideClient(new ClientOptions
// {
//     ApiUrl = "http://localhost:8080",
//     ApiKey = "lp_your_api_key_here",
//     Debug = true
// });

// Basic logging
client.Debug("example", "This is a debug message");
client.Info("example", "Application started");
client.Warn("example", "This is a warning");

// Logging with metadata
client.Info("example", "User logged in", new Dictionary<string, object?>
{
    ["userId"] = 12345,
    ["email"] = "user@example.com",
    ["role"] = "admin"
});

// Error logging with exception
try
{
    throw new InvalidOperationException("Something went wrong!");
}
catch (Exception ex)
{
    client.Error("example", "An error occurred", ex);
}

// Scoped tracing with LogTideScope
using (var scope = LogTideScope.Create())
{
    client.Info("example", "This log has an auto-generated W3C trace ID");
    Console.WriteLine($"Trace ID: {scope.TraceId}");
}

// Get metrics
var metrics = client.GetMetrics();
Console.WriteLine($"\n--- Metrics ---");
Console.WriteLine($"Logs sent: {metrics.LogsSent}");
Console.WriteLine($"Circuit breaker state: {client.GetCircuitBreakerState()}");

Console.WriteLine("\nDone!");
