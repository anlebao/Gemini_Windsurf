using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.KhachLinkAggregate
{
    /// <summary>
    /// KhachLink instance — 1 deployment entry point với profile + nav flags riêng.
    /// Platform-level routing entity (follows ShopInstance pattern):
    /// - TenantId = Guid.Empty (platform sentinel, excluded from multi-tenancy query filter)
    /// - No business key VO (Single-Identity Pattern, Id = PK only)
    /// - Not AggregateRoot (no domain events — routing config entity)
    ///
    /// OwnerTenantId:
    /// - null = platform-level instance (Type 1 directory, Type 2 logistics, Type 3 jobs)
    /// - non-null = tenant-owned instance (Type 4 full commerce, Type 5 reseller)
    ///
    /// Resolved at runtime by CustomDomain (nginx Host header → GET /api/v1/khachlink-instances/by-domain/{domain}).
    /// </summary>
    public class KhachLinkInstance : BaseEntity
    {
        /// <summary>Human-readable label — "Danh bạ Vạn An", "KhachLink Shop A"</summary>
        public string Label { get; private set; } = string.Empty;

        /// <summary>Profile type — defines feature set + default nav flags</summary>
        public KhachLinkProfile Profile { get; private set; } = KhachLinkProfile.FullCommerce;

        /// <summary>
        /// Custom domain for this instance — "diemthuong2.khachvip.online", "shopA.khachvip.online".
        /// nginx routes by Host header. Must be unique across all instances. Stored lowercase.
        /// </summary>
        public string CustomDomain { get; private set; } = string.Empty;

        /// <summary>
        /// Owner tenant — null for platform-level instances (Type 1, 2, 3).
        /// Non-null for tenant-owned instances (Type 4, 5).
        /// When non-null, KhachLink uses this tenant as default context (instead of LastInteractionService).
        /// Note: This is distinct from BaseEntity.TenantId (which is always Guid.Empty — platform sentinel).
        /// </summary>
        public Guid? OwnerTenantId { get; private set; }

        /// <summary>Nav item visibility — owned entity, stored in same table (15 flattened bool columns)</summary>
        public KhachLinkNavFlags NavFlags { get; private set; } = new();

        /// <summary>Whether this instance is active and serving traffic.</summary>
        public bool IsActive { get; private set; } = true;

        // EF Core materialization
        private KhachLinkInstance() { }

        /// <summary>
        /// Factory: create a new KhachLinkInstance.
        /// TenantId is always Guid.Empty (platform sentinel) — this entity is NOT tenant-scoped.
        /// NavFlags defaults to ForProfile(profile) if not overridden.
        /// </summary>
        public KhachLinkInstance(
            string label,
            KhachLinkProfile profile,
            string customDomain,
            Guid? ownerTenantId = null,
            KhachLinkNavFlags? navFlagsOverride = null)
            : base(new TenantId(Guid.Empty)) // platform-level entity, not tenant-scoped
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label cannot be empty.", nameof(label));
            if (string.IsNullOrWhiteSpace(customDomain))
                throw new ArgumentException("CustomDomain cannot be empty.", nameof(customDomain));

            Label = label;
            Profile = profile;
            CustomDomain = customDomain.ToLowerInvariant();
            OwnerTenantId = ownerTenantId;
            NavFlags = navFlagsOverride ?? KhachLinkNavFlags.ForProfile(profile);
            IsActive = true;
        }

        /// <summary>
        /// Update profile + reset nav flags to preset (or override).
        /// </summary>
        public void UpdateProfile(KhachLinkProfile profile, KhachLinkNavFlags? navFlagsOverride = null)
        {
            Profile = profile;
            NavFlags = navFlagsOverride ?? KhachLinkNavFlags.ForProfile(profile);
            UpdateAudit();
        }

        /// <summary>
        /// Override individual nav flags (without changing profile).
        /// </summary>
        public void UpdateNavFlags(KhachLinkNavFlags flags)
        {
            NavFlags = flags ?? throw new ArgumentNullException(nameof(flags));
            UpdateAudit();
        }

        /// <summary>Activate this instance (serve traffic).</summary>
        public void Activate()
        {
            IsActive = true;
            UpdateAudit();
        }

        /// <summary>Deactivate this instance (soft delete — stop serving traffic, keep record).</summary>
        public void Deactivate()
        {
            IsActive = false;
            UpdateAudit();
        }

        /// <summary>Update the display label.</summary>
        public void UpdateLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label cannot be empty.", nameof(label));
            Label = label;
            UpdateAudit();
        }
    }
}
