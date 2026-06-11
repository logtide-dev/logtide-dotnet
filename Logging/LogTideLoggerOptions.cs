using Microsoft.Extensions.Logging;

namespace LogTide.SDK.Logging;

/// <summary>
/// Options for the Microsoft.Extensions.Logging provider.
/// </summary>
public sealed class LogTideLoggerOptions
{
    /// <summary>
    /// Service name stamped on every entry produced by the provider.
    /// </summary>
    public string Service { get; set; } = "dotnet";

    /// <summary>
    /// Minimum level forwarded to LogTide. Defaults to Information.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Whether values from <c>ILogger.BeginScope</c> are merged into entry
    /// metadata. Defaults to true.
    /// </summary>
    public bool IncludeScopes { get; set; } = true;
}
