using LogTide.SDK.Core;
using LogTide.SDK.Models;

// Query API example

Console.WriteLine("LogTide SDK - Query API Example");
Console.WriteLine("================================\n");

await using var client = LogTideClient.FromDsn("https://lp_your_api_key@api.logtide.dev");

// First, send some test logs
Console.WriteLine("Sending test logs...");
for (int i = 0; i < 10; i++)
{
    client.Info("query-example", $"Test log message {i + 1}", new Dictionary<string, object?>
    {
        ["index"] = i,
        ["timestamp"] = DateTime.UtcNow
    });
}

await client.FlushAsync();
Console.WriteLine("Test logs sent.\n");

// Wait for indexing
await Task.Delay(1000);

// 1. Basic query
Console.WriteLine("1. Basic Query");
try
{
    var result = await client.QueryAsync(new QueryOptions
    {
        Service = "query-example",
        Limit = 5
    });

    Console.WriteLine($"  Found {result.Total} logs, showing {result.Logs.Count}:");
    foreach (var log in result.Logs)
    {
        Console.WriteLine($"    [{log.Level}] {log.Message}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Error: {ex.Message}");
}

// 2. Query with filters
Console.WriteLine("\n2. Query with Filters");
try
{
    var result = await client.QueryAsync(new QueryOptions
    {
        Service = "query-example",
        Level = LogTide.SDK.Enums.LogLevel.Info,
        From = DateTime.UtcNow.AddHours(-1),
        To = DateTime.UtcNow,
        Limit = 10
    });

    Console.WriteLine($"  Found {result.Total} info logs in the last hour");
}
catch (Exception ex)
{
    Console.WriteLine($"  Error: {ex.Message}");
}

// 3. Full-text search
Console.WriteLine("\n3. Full-Text Search");
try
{
    var result = await client.QueryAsync(new QueryOptions
    {
        Query = "message 5",
        Limit = 10
    });

    Console.WriteLine($"  Found {result.Total} logs matching 'message 5'");
}
catch (Exception ex)
{
    Console.WriteLine($"  Error: {ex.Message}");
}

// 4. Get logs by trace ID
Console.WriteLine("\n4. Logs by Trace ID");
using (var scope = LogTideScope.Create())
{
    client.Info("query-example", "Step 1: Start");
    client.Info("query-example", "Step 2: Processing");
    client.Info("query-example", "Step 3: Complete");

    await client.FlushAsync();
    await Task.Delay(500);

    try
    {
        var traceLogs = await client.GetByTraceIdAsync(scope.TraceId);
        Console.WriteLine($"  Found {traceLogs.Count} logs for trace {scope.TraceId}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Error: {ex.Message}");
    }
}

// 5. Aggregated statistics
Console.WriteLine("\n5. Aggregated Statistics");
try
{
    var stats = await client.GetAggregatedStatsAsync(new AggregatedStatsOptions
    {
        From = DateTime.UtcNow.AddDays(-7),
        To = DateTime.UtcNow,
        Interval = "1h"
    });

    Console.WriteLine($"  Time series entries: {stats.Timeseries.Count}");
    Console.WriteLine($"  Top services: {stats.TopServices.Count}");
    Console.WriteLine($"  Top errors: {stats.TopErrors.Count}");

    if (stats.TopServices.Count > 0)
    {
        Console.WriteLine("  Top services:");
        foreach (var service in stats.TopServices.Take(5))
        {
            Console.WriteLine($"    - {service.Service}: {service.Count} logs");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Error: {ex.Message}");
}

Console.WriteLine("\nDone!");
