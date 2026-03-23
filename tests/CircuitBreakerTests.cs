using Xunit;
using LogTide.SDK.Enums;
using LogTide.SDK.Internal;

namespace LogTide.SDK.Tests;

public class CircuitBreakerTests
{
    [Fact]
    public void NewCircuitBreaker_IsInClosedState()
    {
        // Arrange & Act
        var breaker = new CircuitBreaker(3, 1000);

        // Assert
        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.True(breaker.CanAttempt());
    }

    [Fact]
    public void RecordSuccess_ResetsFailureCount()
    {
        // Arrange
        var breaker = new CircuitBreaker(3, 1000);
        breaker.RecordFailure();
        breaker.RecordFailure();

        // Act
        breaker.RecordSuccess();

        // Assert
        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.True(breaker.CanAttempt());
    }

    [Fact]
    public void RecordFailure_OpensCircuitAtThreshold()
    {
        // Arrange
        var breaker = new CircuitBreaker(3, 1000);

        // Act
        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.RecordFailure(); // Threshold reached

        // Assert
        Assert.Equal(CircuitState.Open, breaker.State);
        Assert.False(breaker.CanAttempt());
    }

    [Fact]
    public void CircuitBreaker_TransitionsToHalfOpenAfterTimeout()
    {
        // Arrange
        var breaker = new CircuitBreaker(1, 50); // 50ms reset time
        breaker.RecordFailure(); // Open circuit

        // Act
        Thread.Sleep(100); // Wait for reset timeout

        // Assert
        Assert.True(breaker.CanAttempt());
        Assert.Equal(CircuitState.HalfOpen, breaker.State);
    }

    [Fact]
    public void HalfOpen_RecordSuccess_ClosesCircuit()
    {
        // Arrange
        var breaker = new CircuitBreaker(1, 50);
        breaker.RecordFailure();
        Thread.Sleep(100);
        _ = breaker.CanAttempt(); // Transition to half-open

        // Act
        breaker.RecordSuccess();

        // Assert
        Assert.Equal(CircuitState.Closed, breaker.State);
    }

    [Fact]
    public void HalfOpen_RecordFailure_ReopensCircuit()
    {
        // Arrange
        var breaker = new CircuitBreaker(1, 50);
        breaker.RecordFailure();
        Thread.Sleep(100);
        _ = breaker.CanAttempt(); // Transition to half-open

        // Act
        breaker.RecordFailure();

        // Assert
        Assert.Equal(CircuitState.Open, breaker.State);
        Assert.False(breaker.CanAttempt());
    }

    [Fact]
    public void HalfOpen_AllowsOnlyOneProbe()
    {
        var cb = new CircuitBreaker(threshold: 1, resetTimeoutMs: 0);
        cb.RecordFailure(); // opens
        Thread.Sleep(1); // let reset timeout pass

        Assert.True(cb.CanAttempt());  // first probe allowed
        Assert.False(cb.CanAttempt()); // second blocked while probe in-flight
    }

    [Fact]
    public void HalfOpen_FailedProbeReopens()
    {
        var cb = new CircuitBreaker(threshold: 1, resetTimeoutMs: 50);
        cb.RecordFailure();
        Thread.Sleep(100); // let timeout pass to reach HalfOpen
        cb.CanAttempt(); // allow probe
        cb.RecordFailure(); // probe failed → reopen
        Assert.False(cb.CanAttempt()); // should be Open, not HalfOpen yet
    }
}
