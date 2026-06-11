namespace LogTide.SDK.Exceptions;

/// <summary>
/// Base exception for LogTide SDK errors.
/// </summary>
public class LogTideException : Exception
{
    /// <summary>
    /// Creates a new LogTideException.
    /// </summary>
    public LogTideException() : base() { }

    /// <summary>
    /// Creates a new LogTideException with a message.
    /// </summary>
    public LogTideException(string message) : base(message) { }

    /// <summary>
    /// Creates a new LogTideException with a message and inner exception.
    /// </summary>
    public LogTideException(string message, Exception innerException) 
        : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when the log buffer is full.
/// </summary>
public class BufferFullException : LogTideException
{
    /// <summary>
    /// Creates a new BufferFullException.
    /// </summary>
    public BufferFullException() 
        : base("Log buffer is full. Logs are being dropped.") { }

    /// <summary>
    /// Creates a new BufferFullException with a custom message.
    /// </summary>
    public BufferFullException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when the circuit breaker is open.
/// </summary>
public class CircuitBreakerOpenException : LogTideException
{
    /// <summary>
    /// Creates a new CircuitBreakerOpenException.
    /// </summary>
    public CircuitBreakerOpenException() 
        : base("Circuit breaker is open. Requests are being blocked.") { }

    /// <summary>
    /// Creates a new CircuitBreakerOpenException with a custom message.
    /// </summary>
    public CircuitBreakerOpenException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when an API request fails.
/// </summary>
public class ApiException : LogTideException
{
    /// <summary>
    /// HTTP status code of the failed request.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Parsed Retry-After header in milliseconds, when present.
    /// </summary>
    public int? RetryAfterMs { get; init; }

    /// <summary>
    /// Whether the failure is retryable per spec 002 §6 (408, 429, 5xx).
    /// Other 4xx are permanent and must not be retried.
    /// </summary>
    public bool IsRetryable => StatusCode == 408 || StatusCode == 429 || StatusCode >= 500;

    /// <summary>
    /// Creates a new ApiException.
    /// </summary>
    public ApiException(int statusCode, string message) 
        : base($"API request failed with status {statusCode}: {message}")
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Creates a new ApiException with an inner exception.
    /// </summary>
    public ApiException(int statusCode, string message, Exception innerException) 
        : base($"API request failed with status {statusCode}: {message}", innerException)
    {
        StatusCode = statusCode;
    }
}
