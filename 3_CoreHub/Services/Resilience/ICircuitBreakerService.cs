namespace VanAn.CoreHub.Services.Resilience;

/// <summary>
/// Circuit Breaker State
/// </summary>
public enum CircuitBreakerState
{
    Closed,    // Normal operation
    Open,      // Circuit is open, requests blocked
    HalfOpen   // Testing if circuit should close
}

/// <summary>
/// ICircuitBreakerService - Circuit breaker pattern for provider resilience
/// Prevents cascade failures and enables safe failover
/// </summary>
public interface ICircuitBreakerService
{
    /// <summary>
    /// Record a failure for the provider
    /// </summary>
    void RecordFailure(string providerId);

    /// <summary>
    /// Record a success for the provider
    /// </summary>
    void RecordSuccess(string providerId);

    /// <summary>
    /// Check if circuit is open for provider
    /// </summary>
    bool IsOpen(string providerId);

    /// <summary>
    /// Get current circuit state for provider
    /// </summary>
    CircuitBreakerState GetState(string providerId);

    /// <summary>
    /// Reset circuit for provider (after cooldown)
    /// </summary>
    void Reset(string providerId);

    /// <summary>
    /// Get failure count for provider
    /// </summary>
    int GetFailureCount(string providerId);
}
