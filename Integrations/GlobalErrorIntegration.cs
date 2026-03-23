using LogTide.SDK.Core;

namespace LogTide.SDK.Integrations;

public sealed class GlobalErrorIntegration : IIntegration
{
    private volatile ILogTideClient? _client;
    public string Name => "GlobalError";

    public void Setup(ILogTideClient client)
    {
        _client = client;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public void Teardown()
    {
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            _client?.Critical("global", "Unhandled exception", ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _client?.Error("global", "Unobserved task exception", e.Exception);
        e.SetObserved();
    }

    internal void SimulateUnobservedTaskException(AggregateException ex)
        => OnUnobservedTaskException(null, new UnobservedTaskExceptionEventArgs(ex));
}
