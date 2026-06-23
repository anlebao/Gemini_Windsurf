using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.TenantAggregate
{
    /// <summary>
    /// Tenant Aggregate Root — Rich Domain Model.
    /// Replaces the anemic <see cref="VanAn.Shared.Domain.Tenant"/> record (marked [Obsolete] in Domain.cs).
    /// Wave 5: God File split + lifecycle management.
    /// </summary>
    public class Tenant : AggregateRoot
    {
        // ── Identity ──────────────────────────────────────────────────────────
        public new TenantId Id { get; private set; } = null!;
        public string Name { get; private set; } = string.Empty;
        public BusinessType BusinessType { get; private set; }
        public HKDGroup? HKDGroup { get; private set; }

        // ── Lifecycle ─────────────────────────────────────────────────────────
        public TenantStatus Status { get; private set; } = TenantStatus.Active;

        // ── Settings (owned value object) ─────────────────────────────────────
        public TenantSettings Settings { get; private set; } = TenantSettings.Empty();

        // EF Core requires parameterless constructor
        private Tenant() { }

        // ── Factory methods ───────────────────────────────────────────────────

        /// <summary>Creates a new Company tenant and raises TenantCreatedEvent.</summary>
        public static Tenant CreateCompany(TenantId id, string name, TenantSettings? settings = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            var tenant = new Tenant
            {
                Id = id,
                Name = name,
                BusinessType = BusinessType.Company,
                Status = TenantStatus.Active,
                Settings = settings ?? TenantSettings.Empty()
            };
            tenant.SetTenantId(id); // satisfy BaseEntity.TenantId (self-referential for Tenant)
            tenant.AddDomainEvent(new TenantCreatedEvent(id.Value, name, settings?.ContactEmail, DateTime.UtcNow));
            return tenant;
        }

        /// <summary>Creates a new Household Business tenant and raises TenantCreatedEvent.</summary>
        public static Tenant CreateHouseholdBusiness(TenantId id, string name, HKDGroup hkdGroup, TenantSettings? settings = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            var tenant = new Tenant
            {
                Id = id,
                Name = name,
                BusinessType = BusinessType.HouseholdBusiness,
                HKDGroup = hkdGroup,
                Status = TenantStatus.Active,
                Settings = settings ?? TenantSettings.Empty()
            };
            tenant.SetTenantId(id);
            tenant.AddDomainEvent(new TenantCreatedEvent(id.Value, name, settings?.ContactEmail, DateTime.UtcNow));
            return tenant;
        }

        // ── Domain Methods ────────────────────────────────────────────────────

        /// <summary>Suspend tenant. Cannot suspend an already suspended or inactive tenant.</summary>
        public void Suspend(string reason)
        {
            if (Status == TenantStatus.Inactive)
                throw new InvalidOperationException("Cannot suspend an inactive tenant.");
            if (Status == TenantStatus.Suspended)
                throw new InvalidOperationException("Tenant is already suspended.");

            Status = TenantStatus.Suspended;
            UpdateAudit();
            AddDomainEvent(new TenantSuspendedEvent(Id.Value, reason, DateTime.UtcNow));
        }

        /// <summary>Reactivate a suspended tenant.</summary>
        public void Reactivate()
        {
            if (Status != TenantStatus.Suspended)
                throw new InvalidOperationException("Only suspended tenants can be reactivated.");

            Status = TenantStatus.Active;
            UpdateAudit();
        }

        /// <summary>Permanently deactivate tenant. Cannot deactivate an already inactive tenant.</summary>
        public void Deactivate(string reason)
        {
            if (Status == TenantStatus.Inactive)
                throw new InvalidOperationException("Tenant is already inactive.");

            Status = TenantStatus.Inactive;
            UpdateAudit();
            AddDomainEvent(new TenantDeactivatedEvent(Id.Value, reason, DateTime.UtcNow));
        }

        /// <summary>Update tenant profile (name + settings).</summary>
        public void UpdateProfile(string name, TenantSettings settings)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (Status == TenantStatus.Inactive)
                throw new InvalidOperationException("Cannot update profile of an inactive tenant.");

            Name = name;
            Settings = settings;
            UpdateAudit();
        }

        // ── Query helpers ─────────────────────────────────────────────────────
        public bool IsActive() => Status == TenantStatus.Active;
        public bool IsSuspended() => Status == TenantStatus.Suspended;
        public bool IsHouseholdBusiness() => BusinessType == BusinessType.HouseholdBusiness;
        public bool IsCompany() => BusinessType == BusinessType.Company;
    }
}
