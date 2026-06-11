using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LogTide.SDK.Core;

namespace LogTide.SDK.Logging;

/// <summary>
/// ILoggingBuilder extensions for routing Microsoft.Extensions.Logging
/// output to LogTide.
/// </summary>
public static class LogTideLoggingBuilderExtensions
{
    /// <summary>
    /// Adds the LogTide logger provider using an explicit client instance.
    /// </summary>
    public static ILoggingBuilder AddLogTide(
        this ILoggingBuilder builder,
        ILogTideClient client,
        Action<LogTideLoggerOptions>? configure = null)
    {
        var options = new LogTideLoggerOptions();
        configure?.Invoke(options);
        builder.AddProvider(new LogTideLoggerProvider(client, options));
        return builder;
    }

    /// <summary>
    /// Adds the LogTide logger provider resolving the <see cref="ILogTideClient"/>
    /// registered in DI (e.g. via <c>services.AddLogTide(...)</c>).
    /// </summary>
    public static ILoggingBuilder AddLogTide(
        this ILoggingBuilder builder,
        Action<LogTideLoggerOptions>? configure = null)
    {
        var options = new LogTideLoggerOptions();
        configure?.Invoke(options);
        builder.Services.AddSingleton<ILoggerProvider>(
            sp => new LogTideLoggerProvider(sp.GetRequiredService<ILogTideClient>(), options));
        return builder;
    }
}
