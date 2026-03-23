using LogTide.SDK.Core;
using LogTide.SDK.Models;
using LogTide.SDK.Transport;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace LogTide.SDK.Middleware;

public static class LogTideExtensions
{
    /// <summary>
    /// Adds LogTide client as a singleton service with IHttpClientFactory.
    /// </summary>
    public static IServiceCollection AddLogTide(
        this IServiceCollection services,
        ClientOptions options)
    {
        services.AddHttpClient("LogTide", client =>
        {
            client.BaseAddress = new Uri(options.ApiUrl.TrimEnd('/'));
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", options.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
        });

        services.AddSingleton<ILogTideClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new LogTideClient(options, factory);
        });

        return services;
    }

    /// <summary>
    /// Adds LogTide client using an options configuration action.
    /// </summary>
    public static IServiceCollection AddLogTide(
        this IServiceCollection services,
        Action<ClientOptions> configure)
    {
        var options = new ClientOptions();
        configure(options);
        return services.AddLogTide(options);
    }

    /// <summary>
    /// Adds LogTide HTTP request/response logging middleware.
    /// </summary>
    public static IApplicationBuilder UseLogTide(
        this IApplicationBuilder app,
        Action<LogTideMiddlewareOptions>? optionsAction = null)
    {
        var options = new LogTideMiddlewareOptions();
        optionsAction?.Invoke(options);

        return app.UseMiddleware<LogTideMiddleware>(options);
    }

    /// <summary>
    /// Adds LogTide HTTP request/response logging middleware with a service name.
    /// </summary>
    public static IApplicationBuilder UseLogTide(
        this IApplicationBuilder app,
        string serviceName)
    {
        var options = new LogTideMiddlewareOptions { ServiceName = serviceName };
        return app.UseMiddleware<LogTideMiddleware>(options);
    }

    /// <summary>
    /// Adds LogTide error handler middleware that catches unhandled exceptions.
    /// </summary>
    public static IApplicationBuilder UseLogTideErrors(
        this IApplicationBuilder app,
        string serviceName = "aspnet-api")
    {
        return app.UseMiddleware<LogTideErrorHandlerMiddleware>(serviceName);
    }
}
