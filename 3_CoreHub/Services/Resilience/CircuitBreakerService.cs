namespace VanAn.CoreHub.Services.Resilience;

/// <summary>
/// Circuit Breaker State Tracker
/// </summary>
public class CircuitBreakerStateTracker
{
    public CircuitBreakerState State { get; set; } = CircuitBreakerState.Closed;
    public int FailureCount { get; set; }
    public DateTime? LastFailureTime { get; set; }
    public DateTime? OpenedAt { get; set; }
}

/// <summary>
/// CircuitBreakerService - Circuit breaker pattern implementation
/// State transitions: Closed → Open → Half-Open → Closed
/// </summary>
public class CircuitBreakerService : ICircuitBreakerService
{
    private readonly Dictionary<string, CircuitBreakerStateTracker> _trackers = new();
    private readonly int _failureThreshold = 5;
    private readonly TimeSpan _cooldownPeriod = TimeSpan.FromMinutes(5);

    public void RecordFailure(string providerId)
    {
        if (!_trackers.TryGetValue(providerId, out var tracker))
        {
            tracker = new CircuitBreakerStateTracker();
            _trackers[providerId] = tracker;
        }

        tracker.FailureCount++;
        tracker.LastFailureTime = DateTime.UtcNow;

        // Open circuit if threshold exceeded
        if (tracker.FailureCount >= _failureThreshold && tracker.State == CircuitBreakerState.Closed)
        {
            tracker.State = CircuitBreakerState.Open;
            tracker.OpenedAt = DateTime.UtcNow;
        }
    }

    public void RecordSuccess(string providerId)
    {
        if (!_trackers.TryGetValue(providerId, out var tracker))
        {
            tracker = new CircuitBreakerStateTracker();
            _trackers[providerId] = tracker;
        }

        // Reset failure count on success
        tracker.FailureCount = 0;
        tracker.LastFailureTime = null;

        // Close circuit if in Half-Open state
        if (tracker.State == CircuitBreakerState.HalfOpen)
        {
            tracker.State = CircuitBreakerState.Closed;
        }
    }

    public bool IsOpen(string providerId)
    {
        if (!_trackers.TryGetValue(providerId, out var tracker))
            return false;

        // Check if cooldown period has passed
        if (tracker.State == CircuitBreakerState.Open && tracker.OpenedAt.HasValue)
        {
            if (DateTime.UtcNow - tracker.OpenedAt.Value > _cooldownPeriod)
            {
                // Transition to Half-Open for testing
                tracker.State = CircuitBreakerState.HalfOpen;
                return false;
            }
        }

        return tracker.State == CircuitBreakerState.Open;
    }

    public CircuitBreakerState GetState(string providerId)
    {
        if (!_trackers.TryGetValue(providerId, out var tracker))
            return CircuitBreakerState.Closed;

        // Check cooldown period
        if (tracker.State == CircuitBreakerState.Open && tracker.OpenedAt.HasValue)
        {
            if (DateTime.UtcNow - tracker.OpenedAt.Value > _cooldownPeriod)
            {
                tracker.State = CircuitBreakerState.HalfOpen;
            }
        }

        return tracker.State;
    }

    public void Reset(string providerId)
    {
        if (_trackers.TryGetValue(providerId, out var tracker))
        {
            tracker.State = CircuitBreakerState.Closed;
            tracker.FailureCount = 0;
            tracker.LastFailureTime = null;
            tracker.OpenedAt = null;
        }
    }

    public int GetFailureCount(string providerId)
    {
        return _trackers.TryGetValue(providerId, out var tracker) ? tracker.FailureCount : 0;
    }
}
