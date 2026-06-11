using Microsoft.Extensions.Logging;
using LogTide.SDK.Core;
using LogTide.SDK.Models;

namespace LogTide.SDK.Logging;

/// <summary>
/// ILogger implementation routing records through an <see cref="ILogTideClient"/>.
/// Structured state values and scope values become entry metadata; exceptions
/// are serialized to the platform's canonical structured exception format.
/// </summary>
internal sealed class LogTideLogger : ILogger
{
    private readonly ILogTideClient _client;
    private readonly string _category;
    private readonly LogTideLoggerOptions _options;
    private readonly IExternalScopeProvider _scopeProvider;

    public LogTideLogger(
        ILogTideClient client,
        string category,
        LogTideLoggerOptions options,
        IExternalScopeProvider scopeProvider)
    {
        _client = client;
        _category = category;
        _options = options;
        _scopeProvider = scopeProvider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _scopeProvider.Push(state);

    public bool IsEnabled(LogLevel logLevel)
        => logLevel != LogLevel.None && logLevel >= _options.MinimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception == null) return;

        var metadata = new Dictionary<string, object?> { ["logger"] = _category };

        if (_options.IncludeScopes)
        {
            _scopeProvider.ForEachScope((scope, md) => AddStateValues(scope, md), metadata);
        }

        AddStateValues(state, metadata);

        if (eventId.Id != 0)
        {
            metadata["eventId"] = eventId.Id;
        }

        if (exception != null)
        {
            metadata["exception"] = LogTideClient.SerializeException(exception);
        }

        _client.Log(new LogEntry
        {
            Service = _options.Service,
            Level = MapLevel(logLevel),
            Message = string.IsNullOrEmpty(message) ? exception!.Message : message,
            Metadata = metadata,
        });
    }

    private static void AddStateValues(object? state, Dictionary<string, object?> metadata)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> pairs) return;
        foreach (var pair in pairs)
        {
            // {OriginalFormat} is the message template, already rendered into the message
            if (pair.Key == "{OriginalFormat}") continue;
            metadata[pair.Key] = pair.Value;
        }
    }

    private static Enums.LogLevel MapLevel(LogLevel level) => level switch
    {
        LogLevel.Trace or LogLevel.Debug => Enums.LogLevel.Debug,
        LogLevel.Information => Enums.LogLevel.Info,
        LogLevel.Warning => Enums.LogLevel.Warn,
        LogLevel.Error => Enums.LogLevel.Error,
        _ => Enums.LogLevel.Critical,
    };
}
