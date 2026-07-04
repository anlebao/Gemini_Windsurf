using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.TenantAggregate
{
    /// <summary>
    /// Raised when a new Tenant is created — triggers welcome email — Wave 5
    /// </summary>
    public sealed record TenantCreatedEvent(
        Guid TenantId,
        string TenantName,
        string? ContactEmail,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Raised when a Tenant is suspended — Wave 5
    /// </summary>
    public sealed record TenantSuspendedEvent(
        Guid TenantId,
        string Reason,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Raised when a Tenant is permanently deactivated — Wave 5
    /// </summary>
    public sealed record TenantDeactivatedEvent(
        Guid TenantId,
        string Reason,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Raised when an HKD tenant is converted to a DN tenant (D9 Option B).
    /// Wave 2: Domain event only — outbox handler/consumer is W8 scope.
    /// </summary>
    public sealed record TenantConvertedEvent(
        Guid TenantId,           // HKD tenant being converted
        Guid SuccessorTenantId,  // New DN tenant created from conversion
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }
}
