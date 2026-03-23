using LogTide.SDK.Core;
using LogTide.SDK.Middleware;
using LogTide.SDK.Models;

// ASP.NET Core Minimal API example with LogTide middleware

var builder = WebApplication.CreateBuilder(args);

// Add LogTide client with IHttpClientFactory
builder.Services.AddLogTide(new ClientOptions
{
    ApiUrl = builder.Configuration["LogTide:ApiUrl"] ?? "http://localhost:8080",
    ApiKey = builder.Configuration["LogTide:ApiKey"] ?? "lp_your_api_key_here",
    ServiceName = "aspnet-example",
    Debug = builder.Environment.IsDevelopment(),
    GlobalMetadata = new Dictionary<string, object?>
    {
        ["environment"] = builder.Environment.EnvironmentName,
        ["version"] = "1.0.0"
    }
});

var app = builder.Build();

// Add error handler middleware (catches unhandled exceptions)
app.UseLogTideErrors();

// Add LogTide middleware for automatic HTTP logging with W3C traceparent
app.UseLogTide(options =>
{
    options.ServiceName = "aspnet-example";
    options.LogRequests = true;
    options.LogResponses = true;
    options.LogErrors = true;
    options.SkipHealthCheck = true;
    options.SkipPaths.Add("/favicon.ico");
});

// Health check endpoint (skipped by middleware)
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Basic endpoint — ILogTideClient resolved from DI
app.MapGet("/", (ILogTideClient logger) =>
{
    logger.Info("aspnet-example", "Home page accessed");
    return Results.Ok(new { message = "Hello, World!" });
});

// Endpoint with custom logging
app.MapGet("/users/{id}", (int id, ILogTideClient logger) =>
{
    logger.Info("aspnet-example", $"Fetching user {id}", new Dictionary<string, object?>
    {
        ["userId"] = id
    });

    return Results.Ok(new { id, name = $"User {id}", email = $"user{id}@example.com" });
});

// Endpoint that throws an error (caught by UseLogTideErrors)
app.MapGet("/error", () =>
{
    throw new InvalidOperationException("This is a test error!");
});

// Metrics endpoint
app.MapGet("/metrics", (ILogTideClient logger) =>
{
    var metrics = logger.GetMetrics();
    return Results.Ok(new
    {
        logsSent = metrics.LogsSent,
        logsDropped = metrics.LogsDropped,
        errors = metrics.Errors,
        circuitBreakerState = logger.GetCircuitBreakerState().ToString()
    });
});

// Graceful shutdown
app.Lifetime.ApplicationStopping.Register(async () =>
{
    var logger = app.Services.GetRequiredService<ILogTideClient>();
    await logger.FlushAsync();
    Console.WriteLine("Logs flushed on shutdown");
});

app.Run();
