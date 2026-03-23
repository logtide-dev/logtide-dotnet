using System.Diagnostics;
using LogTide.SDK.Enums;
using LogTide.SDK.Exceptions;
using LogTide.SDK.Internal;
using LogTide.SDK.Models;
using LogTide.SDK.Tracing;

namespace LogTide.SDK.Transport;

internal sealed class BatchTransport : IDisposable, IAsyncDisposable
{
    private readonly ILogTransport _logTransport;
    private readonly ISpanTransport? _spanTransport;
    private readonly ClientOptions _options;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly List<LogEntry> _logBuffer = new();
    private readonly List<Span> _spanBuffer = new();
    private readonly object _lock = new();
    private readonly object _metricsLock = new();
    private readonly Timer _flushTimer;
    private readonly List<double> _latencyWindow = new();
    private ClientMetrics _metrics = new();
    private int _disposed; // 0 = not disposed, 1 = disposed; accessed via Interlocked

    public BatchTransport(ILogTransport logTransport, ISpanTransport? spanTransport, ClientOptions options)
    {
        _logTransport = logTransport;
        _spanTransport = spanTransport;
        _options = options;
        _circuitBreaker = new CircuitBreaker(options.CircuitBreakerThreshold, options.CircuitBreakerResetMs);
        _flushTimer = new Timer(_ => FireAndForgetFlush(), null, options.FlushIntervalMs, options.FlushIntervalMs);
    }

    public void Enqueue(LogEntry entry)
    {
        bool shouldFlush;
        lock (_lock)
        {
            if (_logBuffer.Count >= _options.MaxBufferSize)
            {
                lock (_metricsLock) { _metrics.LogsDropped++; }
                throw new BufferFullException();
            }
            _logBuffer.Add(entry);
            shouldFlush = _logBuffer.Count >= _options.BatchSize;
        }
        if (shouldFlush) FireAndForgetFlush();
    }

    public void EnqueueSpan(Span span)
    {
        lock (_lock) { _spanBuffer.Add(span); }
    }

    private void FireAndForgetFlush(object? _ = null)
    {
        if (Volatile.Read(ref _disposed) == 1) return;
        Task.Run(() => FlushAsync()).ContinueWith(t =>
        {
            if (t.IsFaulted && _options.Debug)
                Console.WriteLine($"[LogTide] Flush error: {t.Exception?.GetBaseException().Message}");
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        List<LogEntry> logs;
        List<Span> spans;
        lock (_lock)
        {
            if (_logBuffer.Count == 0 && _spanBuffer.Count == 0) return;
            logs = new List<LogEntry>(_logBuffer);
            spans = new List<Span>(_spanBuffer);
            _logBuffer.Clear();
            _spanBuffer.Clear();
        }

        if (logs.Count > 0) await SendWithRetryAsync(logs, ct).ConfigureAwait(false);
        if (spans.Count > 0 && _spanTransport != null)
            await SendSpansWithRetryAsync(spans, ct).ConfigureAwait(false);
    }

    private async Task SendWithRetryAsync(List<LogEntry> logs, CancellationToken ct)
    {
        var attempt = 0;
        var delay = _options.RetryDelayMs;
        while (attempt <= _options.MaxRetries)
        {
            try
            {
                if (!_circuitBreaker.CanAttempt())
                {
                    lock (_metricsLock) { _metrics.CircuitBreakerTrips++; }
                    break; // drop to the single LogsDropped increment below
                }
                var sw = Stopwatch.StartNew();
                await _logTransport.SendAsync(logs, ct).ConfigureAwait(false);
                sw.Stop();
                _circuitBreaker.RecordSuccess();
                UpdateLatency(sw.Elapsed.TotalMilliseconds);
                lock (_metricsLock) { _metrics.LogsSent += logs.Count; }
                return;
            }
            catch (Exception)
            {
                attempt++;
                lock (_metricsLock) { _metrics.Errors++; if (attempt <= _options.MaxRetries) _metrics.Retries++; }
                if (attempt > _options.MaxRetries)
                {
                    _circuitBreaker.RecordFailure(); // record once after all retries exhausted
                    break;
                }
                await Task.Delay(delay, ct).ConfigureAwait(false);
                delay *= 2;
            }
        }
        lock (_metricsLock) { _metrics.LogsDropped += logs.Count; }
    }

    private async Task SendSpansWithRetryAsync(List<Span> spans, CancellationToken ct)
    {
        try { await _spanTransport!.SendSpansAsync(spans, ct).ConfigureAwait(false); }
        catch (Exception ex)
        {
            if (_options.Debug)
                Console.WriteLine($"[LogTide] Span send error: {ex.Message}");
        }
    }

    private void UpdateLatency(double ms)
    {
        lock (_metricsLock)
        {
            _latencyWindow.Add(ms);
            if (_latencyWindow.Count > 100) _latencyWindow.RemoveAt(0);
            _metrics.AvgLatencyMs = _latencyWindow.Average();
        }
    }

    public ClientMetrics GetMetrics() { lock (_metricsLock) { return _metrics.Clone(); } }
    public void ResetMetrics() { lock (_metricsLock) { _metrics = new(); _latencyWindow.Clear(); } }
    public CircuitState CircuitBreakerState => _circuitBreaker.State;

    public void Dispose()
    {
        // Task.Run avoids deadlock when caller has a SynchronizationContext
        Task.Run(() => DisposeAsync().AsTask()).GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;
        await _flushTimer.DisposeAsync().ConfigureAwait(false);
        await FlushAsync().ConfigureAwait(false);
    }
}
