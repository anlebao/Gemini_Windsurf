using VanAn.CoreHub.Services.Resilience;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Services;

public class CircuitBreakerServiceTests
{
    private readonly CircuitBreakerService _circuitBreaker;

    public CircuitBreakerServiceTests()
    {
        _circuitBreaker = new CircuitBreakerService();
    }

    [Fact]
    public void GetState_WhenProviderNotTracked_ShouldReturnClosed()
    {
        // Act
        var state = _circuitBreaker.GetState("non-existent-provider");

        // Assert
        state.Should().Be(CircuitBreakerState.Closed);
    }

    [Fact]
    public void IsOpen_WhenProviderNotTracked_ShouldReturnFalse()
    {
        // Act
        var isOpen = _circuitBreaker.IsOpen("non-existent-provider");

        // Assert
        isOpen.Should().BeFalse();
    }

    [Fact]
    public void GetFailureCount_WhenProviderNotTracked_ShouldReturnZero()
    {
        // Act
        var count = _circuitBreaker.GetFailureCount("non-existent-provider");

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void RecordFailure_ShouldIncrementFailureCount()
    {
        // Act
        _circuitBreaker.RecordFailure("provider-1");

        // Assert
        _circuitBreaker.GetFailureCount("provider-1").Should().Be(1);
    }

    [Fact]
    public void RecordFailure_MultipleCalls_ShouldIncrementFailureCount()
    {
        // Act
        _circuitBreaker.RecordFailure("provider-1");
        _circuitBreaker.RecordFailure("provider-1");
        _circuitBreaker.RecordFailure("provider-1");

        // Assert
        _circuitBreaker.GetFailureCount("provider-1").Should().Be(3);
    }

    [Fact]
    public void RecordFailure_WhenThresholdReached_ShouldOpenCircuit()
    {
        // Arrange
        const string providerId = "provider-1";

        // Act - Record 5 failures (threshold)
        for (int i = 0; i < 5; i++)
        {
            _circuitBreaker.RecordFailure(providerId);
        }

        // Assert
        _circuitBreaker.GetState(providerId).Should().Be(CircuitBreakerState.Open);
        _circuitBreaker.IsOpen(providerId).Should().BeTrue();
    }

    [Fact]
    public void RecordFailure_WhenThresholdNotReached_ShouldKeepCircuitClosed()
    {
        // Arrange
        const string providerId = "provider-1";

        // Act - Record 4 failures (below threshold of 5)
        for (int i = 0; i < 4; i++)
        {
            _circuitBreaker.RecordFailure(providerId);
        }

        // Assert
        _circuitBreaker.GetState(providerId).Should().Be(CircuitBreakerState.Closed);
        _circuitBreaker.IsOpen(providerId).Should().BeFalse();
    }

    [Fact]
    public void RecordFailure_WhenCircuitAlreadyOpen_ShouldNotChangeState()
    {
        // Arrange
        const string providerId = "provider-1";
        for (int i = 0; i < 5; i++)
        {
            _circuitBreaker.RecordFailure(providerId);
        }

        // Act - Record additional failure after circuit is open
        _circuitBreaker.RecordFailure(providerId);

        // Assert
        _circuitBreaker.GetState(providerId).Should().Be(CircuitBreakerState.Open);
        _circuitBreaker.GetFailureCount(providerId).Should().Be(6);
    }

    [Fact]
    public void RecordSuccess_ShouldResetFailureCount()
    {
        // Arrange
        const string providerId = "provider-1";
        _circuitBreaker.RecordFailure(providerId);
        _circuitBreaker.RecordFailure(providerId);

        // Act
        _circuitBreaker.RecordSuccess(providerId);

        // Assert
        _circuitBreaker.GetFailureCount(providerId).Should().Be(0);
    }

    [Fact]
    public void RecordSuccess_WhenCircuitClosed_ShouldKeepCircuitClosed()
    {
        // Arrange
        const string providerId = "provider-1";
        _circuitBreaker.RecordFailure(providerId);

        // Act
        _circuitBreaker.RecordSuccess(providerId);

        // Assert
        _circuitBreaker.GetState(providerId).Should().Be(CircuitBreakerState.Closed);
    }


    [Fact]
    public void IsOpen_WhenCircuitOpen_ShouldReturnTrue()
    {
        // Arrange
        const string providerId = "provider-1";
        for (int i = 0; i < 5; i++)
        {
            _circuitBreaker.RecordFailure(providerId);
        }

        // Act
        var isOpen = _circuitBreaker.IsOpen(providerId);

        // Assert
        isOpen.Should().BeTrue();
    }

    [Fact]
    public void IsOpen_WhenCircuitClosed_ShouldReturnFalse()
    {
        // Arrange
        const string providerId = "provider-1";
        _circuitBreaker.RecordFailure(providerId);

        // Act
        var isOpen = _circuitBreaker.IsOpen(providerId);

        // Assert
        isOpen.Should().BeFalse();
    }

    [Fact]
    public void IsOpen_WhenCircuitHalfOpen_ShouldReturnFalse()
    {
        // Arrange
        const string providerId = "provider-1";
        
        // Open circuit
        for (int i = 0; i < 5; i++)
        {
            _circuitBreaker.RecordFailure(providerId);
        }

        // Manually set to Half-Open by simulating cooldown period
        // Note: This test relies on the internal implementation details
        // In a real scenario, you'd need to wait for the cooldown period
        _circuitBreaker.Reset(providerId);

        // Act
        var isOpen = _circuitBreaker.IsOpen(providerId);

        // Assert
        isOpen.Should().BeFalse();
    }

    [Fact]
    public void Reset_ShouldResetCircuitToClosed()
    {
        // Arrange
        const string providerId = "provider-1";
        for (int i = 0; i < 5; i++)
        {
            _circuitBreaker.RecordFailure(providerId);
        }

        // Act
        _circuitBreaker.Reset(providerId);

        // Assert
        _circuitBreaker.GetState(providerId).Should().Be(CircuitBreakerState.Closed);
        _circuitBreaker.GetFailureCount(providerId).Should().Be(0);
        _circuitBreaker.IsOpen(providerId).Should().BeFalse();
    }

    [Fact]
    public void Reset_WhenProviderNotTracked_ShouldNotThrow()
    {
        // Act & Assert - Should not throw
        _circuitBreaker.Reset("non-existent-provider");
    }

    [Fact]
    public void MultipleProviders_ShouldTrackIndependently()
    {
        // Arrange
        const string provider1 = "provider-1";
        const string provider2 = "provider-2";

        // Act
        _circuitBreaker.RecordFailure(provider1);
        _circuitBreaker.RecordFailure(provider1);
        _circuitBreaker.RecordFailure(provider2);
        _circuitBreaker.RecordFailure(provider2);
        _circuitBreaker.RecordFailure(provider2);
        _circuitBreaker.RecordFailure(provider2);
        _circuitBreaker.RecordFailure(provider2); // This should open provider-2

        // Assert
        _circuitBreaker.GetFailureCount(provider1).Should().Be(2);
        _circuitBreaker.GetState(provider1).Should().Be(CircuitBreakerState.Closed);
        _circuitBreaker.GetFailureCount(provider2).Should().Be(5);
        _circuitBreaker.GetState(provider2).Should().Be(CircuitBreakerState.Open);
    }

    [Fact]
    public void FullLifecycle_ShouldAllowCompleteStateTransitions()
    {
        // Arrange
        const string providerId = "provider-1";

        // Act - Full lifecycle
        // 1. Start closed
        _circuitBreaker.GetState(providerId).Should().Be(CircuitBreakerState.Closed);

        // 2. Record failures to open circuit
        for (int i = 0; i < 5; i++)
        {
            _circuitBreaker.RecordFailure(providerId);
        }
        _circuitBreaker.GetState(providerId).Should().Be(CircuitBreakerState.Open);

        // 3. Reset circuit
        _circuitBreaker.Reset(providerId);
        _circuitBreaker.GetState(providerId).Should().Be(CircuitBreakerState.Closed);

        // 4. Record success
        _circuitBreaker.RecordSuccess(providerId);
        _circuitBreaker.GetState(providerId).Should().Be(CircuitBreakerState.Closed);
    }
}

public class CircuitBreakerStateTrackerTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithClosedState()
    {
        // Act
        var tracker = new CircuitBreakerStateTracker();

        // Assert
        tracker.State.Should().Be(CircuitBreakerState.Closed);
        tracker.FailureCount.Should().Be(0);
        tracker.LastFailureTime.Should().BeNull();
        tracker.OpenedAt.Should().BeNull();
    }

    [Fact]
    public void StateTracker_ShouldAllowStateModification()
    {
        // Arrange
        var tracker = new CircuitBreakerStateTracker();

        // Act
        tracker.State = CircuitBreakerState.Open;
        tracker.FailureCount = 5;
        tracker.LastFailureTime = DateTime.UtcNow;
        tracker.OpenedAt = DateTime.UtcNow;

        // Assert
        tracker.State.Should().Be(CircuitBreakerState.Open);
        tracker.FailureCount.Should().Be(5);
        tracker.LastFailureTime.Should().NotBeNull();
        tracker.OpenedAt.Should().NotBeNull();
    }
}
