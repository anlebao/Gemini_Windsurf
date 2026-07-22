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

        // C1 fix 2026-07-04: TenantType classification for feature flag routing (W8).
        // HKD tenants: Type=HKD (set via CreateHouseholdBusiness below).
        // DN tenants: Type=Enterprise_* (set via CreateFromConversion).
        // DN created directly via CreateCompany: Type=null until W8 SetTenantType() method added.
        public TenantType? Type { get; private set; }

        // Phase 1 (Multi-VPS Checkout): FK to ShopInstance — which VPS hosts this tenant's ShopERP.
        // Nullable for backward compat — existing tenants get backfilled in migration.
        public Guid? ShopInstanceId { get; private set; }

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
                Settings = settings ?? TenantSettings.Empty(),
                Type = TenantType.HKD  // C1 fix: classify HKD tenants for W8 feature flag routing
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
        /// H3 fix: predecessorTenantId must not be empty (Guid.Empty) — would create untraceable link.
        /// </summary>
        public static Tenant CreateFromConversion(
            TenantId newId, string name, TenantType newType,
            TenantId predecessorTenantId, AccountingStandard standard,
            TenantSettings? settings = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (predecessorTenantId.IsEmpty())
                throw new ArgumentException("Predecessor tenant id cannot be empty.", nameof(predecessorTenantId));
            if (newType == TenantType.HKD)
                throw new ArgumentException("Conversion target type must be an Enterprise type, not HKD.", nameof(newType));

            var tenant = new Tenant
            {
                Id = newId,
                Name = name,
                BusinessType = BusinessType.Company,  // DN is always Company
                Status = TenantStatus.Active,
                Settings = settings ?? TenantSettings.Empty(),
                PredecessorTenantId = predecessorTenantId,
                ConvertedAt = DateTime.UtcNow,
                AccountingStandard = standard,
                Type = newType  // C1 fix: use newType parameter (was dead code before)
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
        /// Tenant Profile Page (2026-07-21): Update URL slug for /store/{slug} route.
        /// Slug must be lowercase, alphanumeric + hyphens, max 100 chars. Null clears the slug.
        /// Uniqueness is enforced at the infrastructure layer (unique index in TenantConfiguration).
        /// </summary>
        public void UpdateSlug(string? slug)
        {
            if (Status == TenantStatus.Inactive)
                throw new InvalidOperationException("Cannot update slug of an inactive tenant.");

            if (!string.IsNullOrWhiteSpace(slug))
            {
                slug = slug.Trim().ToLowerInvariant();
                if (slug.Length > 100)
                    throw new ArgumentException("Slug must be at most 100 characters.", nameof(slug));
                if (!System.Text.RegularExpressions.Regex.IsMatch(slug, @"^[a-z0-9]+(?:-[a-z0-9]+)*$"))
                    throw new ArgumentException("Slug must be lowercase, alphanumeric, hyphen-separated.", nameof(slug));
            }
            else
            {
                slug = null;
            }

            Settings = Settings.WithSlug(slug);
            UpdateAudit();
        }

        /// <summary>
        /// W8 (H4 deferred from W2): Set TenantType + AccountingStandard for feature flag routing.
        /// Used to classify existing tenants created via CreateCompany (which doesn't set Type).
        /// Cannot change Type of an already-classified tenant (one-way classification).
        /// </summary>
        public void SetTenantType(TenantType type, AccountingStandard? standard = null)
        {
            if (Status == TenantStatus.Inactive)
                throw new InvalidOperationException("Cannot update tenant type of an inactive tenant.");
            if (Type is not null && Type != type)
                throw new InvalidOperationException($"Tenant is already classified as {Type}. Cannot change to {type}.");

            Type = type;
            if (standard is not null)
                AccountingStandard = standard;
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
        /// Sets Status=Converted (read-only historical) + links successor + records conversion timestamp.
        /// Raises TenantConvertedEvent.
        ///
        /// Guards:
        /// - Inactive tenant cannot convert (archived, no business activity to migrate).
        /// - Already-converted tenant cannot re-convert (one-way conversion per D9).
        /// - H2 decision 2026-07-04: Suspended tenant CAN convert — business rationale:
        ///   HKD bị đình chỉ do nợ/thủ tục, chủ muốn chuyển sang DN để hoạt động lại dưới tư cách pháp nhân mới.
        ///   Conversion tạo DN mới (Active) + HKD cũ thành read-only historical. Nợ/thủ tục của HKD
        ///   không tự động chuyển sang DN — đó là quyết định của cơ quan thuế, không phải hệ thống kế toán.
        /// - H3 fix: successorTenantId must not be empty (Guid.Empty) — would create untraceable link.
        /// </summary>
        public void MarkConvertedTo(TenantId successorTenantId)
        {
            if (Status == TenantStatus.Inactive)
                throw new InvalidOperationException("Cannot convert an inactive tenant.");
            if (Status == TenantStatus.Converted)
                throw new InvalidOperationException("Tenant is already converted.");
            if (successorTenantId.IsEmpty())
                throw new ArgumentException("Successor tenant id cannot be empty.", nameof(successorTenantId));

            Status = TenantStatus.Converted;
            SuccessorTenantId = successorTenantId;
            ConvertedAt = DateTime.UtcNow;  // H1 fix: record conversion timestamp on HKD side (audit trail)
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

        // ── Phase 1: Multi-VPS routing ────────────────────────────────────────

        /// <summary>
        /// Phase 1 (Multi-VPS Checkout): Assign this tenant to a ShopERP hosting instance.
        /// Used by Gateway router to determine which VPS to forward HTTP requests to.
        /// </summary>
        /// <param name="shopInstanceId">The ShopInstance Id (must not be Guid.Empty).</param>
        public void AssignToShopInstance(Guid shopInstanceId)
        {
            if (shopInstanceId == Guid.Empty)
                throw new ArgumentException("ShopInstanceId cannot be empty.", nameof(shopInstanceId));
            ShopInstanceId = shopInstanceId;
            UpdateAudit();
        }
    }
}
