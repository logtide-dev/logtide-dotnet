using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using LogTide.SDK.Core;

namespace LogTide.SDK.Serilog;

public static class LogTideSinkExtensions
{
    public static LoggerConfiguration LogTide(
        this LoggerSinkConfiguration sinkConfiguration,
        ILogTideClient client,
        string serviceName = "app",
        LogEventLevel minimumLevel = LogEventLevel.Verbose) =>
        sinkConfiguration.Sink(new LogTideSink(client, serviceName), minimumLevel);
}
