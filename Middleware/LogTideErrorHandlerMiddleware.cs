using LogTide.SDK.Core;
using Microsoft.AspNetCore.Http;

namespace LogTide.SDK.Middleware;

public class LogTideErrorHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogTideClient _client;
    private readonly string _serviceName;

    public LogTideErrorHandlerMiddleware(RequestDelegate next, ILogTideClient client, string serviceName = "aspnet-api")
    {
        _next = next;
        _client = client;
        _serviceName = serviceName;
    }

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
