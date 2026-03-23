using Serilog.Core;
using Serilog.Events;
using LogTide.SDK.Core;
using LogTide.SDK.Enums;
using LogTide.SDK.Models;

namespace LogTide.SDK.Serilog;

public sealed class LogTideSink : ILogEventSink
{
    private readonly ILogTideClient _client;
    private readonly string _serviceName;

    public LogTideSink(ILogTideClient client, string serviceName = "app")
    {
        _client = client;
        _serviceName = serviceName;
    }

    public void Emit(LogEvent logEvent)
    {
        var metadata = new Dictionary<string, object?>();
        foreach (var p in logEvent.Properties)
            metadata[p.Key] = ExtractValue(p.Value);
        if (logEvent.Exception != null)
            metadata["error"] = SerializeException(logEvent.Exception);

        _client.Log(new LogEntry
        {
            Service = _serviceName,
            Level = MapLevel(logEvent.Level),
            Message = logEvent.RenderMessage(),
            Metadata = metadata
        });
    }

    private static LogLevel MapLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose or LogEventLevel.Debug => LogLevel.Debug,
        LogEventLevel.Information => LogLevel.Info,
        LogEventLevel.Warning => LogLevel.Warn,
        LogEventLevel.Error => LogLevel.Error,
        LogEventLevel.Fatal => LogLevel.Critical,
        _ => LogLevel.Info
    };

    private static object? ExtractValue(LogEventPropertyValue value) => value switch
    {
        ScalarValue sv => sv.Value,
        SequenceValue seq => seq.Elements.Select(ExtractValue).ToArray(),
        StructureValue struc => struc.Properties.ToDictionary(p => p.Name, p => ExtractValue(p.Value) as object),
        DictionaryValue dict => dict.Elements.ToDictionary(
            kv => ExtractValue(kv.Key)?.ToString() ?? string.Empty,
            kv => ExtractValue(kv.Value)),
        _ => value.ToString()
    };

    private static Dictionary<string, object?> SerializeException(Exception ex)
    {
        var r = new Dictionary<string, object?> { ["type"] = ex.GetType().FullName, ["message"] = ex.Message, ["stack"] = ex.StackTrace };
        if (ex.InnerException != null) r["cause"] = SerializeException(ex.InnerException);
        return r;
    }
}
