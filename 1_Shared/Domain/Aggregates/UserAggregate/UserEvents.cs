using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.UserAggregate
{
    /// <summary>
    /// Raised when a new user is created — triggers welcome email — Wave 6.
    /// </summary>
    public sealed record UserCreatedEvent(
        Guid UserId,
        Guid TenantId,
        string Username,
        string DisplayName,
        UserRole Role,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Raised when a user is deactivated — Wave 6.
    /// </summary>
    public sealed record UserDeactivatedEvent(
        Guid UserId,
        Guid TenantId,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Raised when a user changes password — Wave 6.
    /// </summary>
    public sealed record UserPasswordChangedEvent(
        Guid UserId,
        Guid TenantId,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Raised when a user role changes — Wave 6.
    /// </summary>
    public sealed record UserRoleChangedEvent(
        Guid UserId,
        Guid TenantId,
        UserRole PreviousRole,
        UserRole NewRole,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }
}
