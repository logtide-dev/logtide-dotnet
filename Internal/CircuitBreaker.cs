using LogTide.SDK.Enums;

namespace LogTide.SDK.Internal;

/// <summary>
/// Circuit breaker implementation for fault tolerance.
/// </summary>
internal class CircuitBreaker
{
    private readonly int _threshold;
    private readonly int _resetTimeoutMs;
    private readonly object _lock = new();
    
    private CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private DateTime? _lastFailureTime;
    private bool _halfOpenProbePending;

    /// <summary>
    /// Creates a new circuit breaker.
    /// </summary>
    /// <param name="threshold">Number of failures before opening.</param>
    /// <param name="resetTimeoutMs">Time in ms before transitioning to half-open.</param>
    public CircuitBreaker(int threshold, int resetTimeoutMs)
    {
        _threshold = threshold;
        _resetTimeoutMs = resetTimeoutMs;
    }

    /// <summary>
    /// Current state of the circuit breaker.
    /// </summary>
    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                UpdateState();
                return _state;
            }
        }
    }

    /// <summary>
    /// Records a successful operation.
    /// </summary>
    public void RecordSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _halfOpenProbePending = false;
            _state = CircuitState.Closed;
        }
    }

    /// <summary>
    /// Records a failed operation.
    /// </summary>
    public void RecordFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _halfOpenProbePending = false;
            _lastFailureTime = DateTime.UtcNow;
            if (_failureCount >= _threshold) _state = CircuitState.Open;
        }
    }

    /// <summary>
    /// Checks if an operation can be attempted.
    /// </summary>
    public bool CanAttempt()
    {
        lock (_lock)
        {
            UpdateState();
            if (_state == CircuitState.Closed) return true;
            if (_state == CircuitState.Open) return false;
            // HalfOpen: allow exactly one probe
            if (_halfOpenProbePending) return false;
            _halfOpenProbePending = true;
            return true;
        }
    }

    private void UpdateState()
    {
        if (_state == CircuitState.Open && _lastFailureTime.HasValue)
        {
            var elapsed = DateTime.UtcNow - _lastFailureTime.Value;
            if (elapsed.TotalMilliseconds >= _resetTimeoutMs)
            {
                _state = CircuitState.HalfOpen;
            }
        }
    }
}
