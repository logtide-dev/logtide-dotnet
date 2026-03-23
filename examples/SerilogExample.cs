using LogTide.SDK.Core;
using LogTide.SDK.Models;
using LogTide.SDK.Serilog;
using Serilog;

// Serilog integration example

Console.WriteLine("LogTide SDK - Serilog Example");
Console.WriteLine("============================\n");

// Create LogTide client
await using var logtideClient = LogTideClient.FromDsn("https://lp_your_key@api.logtide.dev");

// Configure Serilog to write to LogTide
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.LogTide(logtideClient, serviceName: "my-service")
    .CreateLogger();

// Use Serilog as normal — logs are forwarded to LogTide
Log.Information("Application started");
Log.Warning("This is a warning from Serilog");
Log.Error(new InvalidOperationException("oops"), "An error occurred");

// Structured properties are mapped to LogTide metadata
Log.Information("User {UserId} logged in from {IpAddress}", 42, "192.168.1.1");

await logtideClient.FlushAsync();
Log.CloseAndFlush();

Console.WriteLine("\nDone!");
