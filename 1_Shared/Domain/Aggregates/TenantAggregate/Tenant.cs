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

        // Wave 5 (approved 2026-07-03): Default industry sector for HKD Group 2 tenants.
        // Nullable — existing tenants get NULL, must be set before generating S2a/S2b.
        // Used as fallback when Order.IndustrySector is not set.
        public IndustrySector? DefaultIndustrySector { get; private set; }

        // ── D9: HKD↔DN Conversion Link (Option B — New Tenant + Link) ─────────
        // Predecessor: Tenant cũ (HKD) mà DN này được convert từ (set on new DN tenant).
        public TenantId? PredecessorTenantId { get; private set; }
        // Successor: Tenant mới (DN) mà HKD này đã convert sang (set on old HKD tenant).
        public TenantId? SuccessorTenantId { get; private set; }
        public DateTime? ConvertedAt { get; private set; }
        // Accounting standard applies to ALL tenants (not just converted ones).
        // HKD = null (TT 152 implied by TenantType=HKD). DN = TT99/133/58.
        // Review 2026-07-04: replaced ConvertedToStandard with general AccountingStandard.
        public AccountingStandard? AccountingStandard { get; private set; }

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

        /// <summary>
        /// D9 Option B: Create a new DN tenant from HKD conversion.
        /// The new tenant links back to its HKD predecessor via PredecessorTenantId.
        /// Raises TenantCreatedEvent (standard lifecycle) — successor link set by caller via MarkConvertedTo.
        /// Note: SetTenantId sets BaseEntity.TenantId (Guid, for multi-tenancy filtering),
        ///       distinct from Tenant.Id (TenantId, strongly-typed) — NOT redundant.
        /// </summary>
        public static Tenant CreateFromConversion(
            TenantId newId, string name, TenantType newType,
            TenantId predecessorTenantId, AccountingStandard standard,
            TenantSettings? settings = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            var tenant = new Tenant
            {
                Id = newId,
                Name = name,
                BusinessType = BusinessType.Company,  // DN is always Company
                Status = TenantStatus.Active,
                Settings = settings ?? TenantSettings.Empty(),
                PredecessorTenantId = predecessorTenantId,
                ConvertedAt = DateTime.UtcNow,
                AccountingStandard = standard
            };
            tenant.SetTenantId(newId); // sets BaseEntity.TenantId (multi-tenancy) — distinct from Tenant.Id
            tenant.AddDomainEvent(new TenantCreatedEvent(newId.Value, name, settings?.ContactEmail, DateTime.UtcNow));
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

        /// <summary>
        /// Wave 5: Set default industry sector for HKD Group 2 reporting (TT 152 S2a/S2b).
        /// Only meaningful for HouseholdBusiness tenants. Used as fallback when Order.IndustrySector is NULL.
        /// </summary>
        public void SetDefaultIndustrySector(IndustrySector? sector)
        {
            if (Status == TenantStatus.Inactive)
                throw new InvalidOperationException("Cannot update industry sector of an inactive tenant.");
            DefaultIndustrySector = sector;
            UpdateAudit();
        }

        /// <summary>
        /// D9 Option B: Mark an HKD tenant as converted to a DN successor.
        /// Sets Status=Converted (read-only historical) + links successor.
        /// Raises TenantConvertedEvent. Cannot convert an inactive or already-converted tenant.
        /// </summary>
        public void MarkConvertedTo(TenantId successorTenantId)
        {
            if (Status == TenantStatus.Inactive)
                throw new InvalidOperationException("Cannot convert an inactive tenant.");
            if (Status == TenantStatus.Converted)
                throw new InvalidOperationException("Tenant is already converted.");
            Status = TenantStatus.Converted;
            SuccessorTenantId = successorTenantId;
            UpdateAudit();
            AddDomainEvent(new TenantConvertedEvent(Id.Value, successorTenantId.Value, DateTime.UtcNow));
        }

        // ── Query helpers ─────────────────────────────────────────────────────
        public bool IsActive() => Status == TenantStatus.Active;
        public bool IsSuspended() => Status == TenantStatus.Suspended;
        public bool IsConverted() => Status == TenantStatus.Converted;
        public bool IsConversionOf(TenantId predecessor) => PredecessorTenantId == predecessor;
        public bool IsHouseholdBusiness() => BusinessType == BusinessType.HouseholdBusiness;
        public bool IsCompany() => BusinessType == BusinessType.Company;
    }
}
