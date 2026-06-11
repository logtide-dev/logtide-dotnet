using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using LogTide.SDK.Core;

namespace LogTide.SDK.Logging;

/// <summary>
/// Microsoft.Extensions.Logging provider routing all ILogger output through
/// an <see cref="ILogTideClient"/>. The provider does not own the client and
/// never disposes it.
/// </summary>
[ProviderAlias("LogTide")]
public sealed class LogTideLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ILogTideClient _client;
    private readonly LogTideLoggerOptions _options;
    private readonly ConcurrentDictionary<string, LogTideLogger> _loggers = new();
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public LogTideLoggerProvider(ILogTideClient client, LogTideLoggerOptions? options = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? new LogTideLoggerOptions();
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new LogTideLogger(_client, name, _options, _scopeProvider));

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        => _scopeProvider = scopeProvider;

    public void Dispose() => _loggers.Clear();
}
