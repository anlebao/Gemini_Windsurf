using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.DomainResellerAggregate
{
    /// <summary>
    /// Tenant-owned domain registered via Vạn An reseller platform.
    /// Tracks the lifecycle of a domain purchased by a tenant (e.g. shopA.com) and
    /// its linkage to a KhachLinkInstance for routing + SSL provisioning.
    ///
    /// Platform-level entity (follows KhachLinkInstance pattern):
    /// - TenantId = Guid.Empty (platform sentinel, excluded from multi-tenancy query filter)
    /// - No business key VO (Single-Identity Pattern, Id = PK only)
    /// - Not AggregateRoot (no domain events — tracking record for reseller operations)
    ///
    /// OwnerTenantId:
    /// - The tenant who purchased the domain. Distinct from BaseEntity.TenantId (which is
    ///   always Guid.Empty — platform sentinel). OwnerTenantId drives admin UI filtering
    ///   and links the domain to the tenant's KhachLinkInstance.
    ///
    /// KhachLinkInstanceId:
    /// - Optional FK to KhachLinkInstances.Id. Set when admin links the domain to a
    ///   KhachLinkInstance for custom-domain routing. Nullable because a tenant may
    ///   purchase a domain without immediately attaching a KhachLink storefront.
    ///
    /// Domain Reseller R1: GoDaddy API verified 2026-08-17 (read + write + delete).
    /// </summary>
    public class TenantDomain : BaseEntity
    {
        /// <summary>Domain name (lowercase, canonicalized) — e.g. "shopa.com".</summary>
        public string Domain { get; private set; } = string.Empty;

        /// <summary>
        /// Registrar provider managing this domain. Stored as int (enum conversion).
        /// Drives which IDomainRegistrarService implementation handles operations.
        /// </summary>
        public RegistrarProvider Registrar { get; private set; } = RegistrarProvider.GoDaddy;

        /// <summary>
        /// Owner tenant — the tenant who purchased the domain via Vạn An reseller.
        /// Distinct from BaseEntity.TenantId (which is always Guid.Empty — platform sentinel).
        /// </summary>
        public Guid OwnerTenantId { get; private set; }

        /// <summary>
        /// Optional FK to KhachLinkInstances.Id. Set when admin links the domain to a
        /// KhachLinkInstance (CustomDomain = this.Domain). Nullable until linked.
        /// </summary>
        public Guid? KhachLinkInstanceId { get; private set; }

        /// <summary>Registration timestamp (UTC) — set by registrar API response.</summary>
        public DateTime RegisteredAt { get; private set; }

        /// <summary>Expiry timestamp (UTC) — drives auto-renew + expiry alerts.</summary>
        public DateTime ExpiresAt { get; private set; }

        /// <summary>Whether auto-renew is enabled at the registrar (default true).</summary>
        public bool AutoRenew { get; private set; } = true;

        /// <summary>Lifecycle status — drives admin UI + cron logic.</summary>
        public DomainStatus Status { get; private set; } = DomainStatus.Pending;

        /// <summary>
        /// Registrant email — required by most registrars for WHOIS + expiry alerts.
        /// Provided by tenant at purchase time. Stored for renewal + transfer operations.
        /// </summary>
        public string RegistrantEmail { get; private set; } = string.Empty;

        /// <summary>
        /// Last registrar operation ID (GoDaddy v3 returns async operation ID for polling).
        /// Null for sync registrars or after operation completes.
        /// </summary>
        public string? LastOperationId { get; private set; }

        /// <summary>
        /// Last error message if Status = Failed or Suspended. Null when operating normally.
        /// </summary>
        public string? LastError { get; private set; }

        // EF Core materialization
        private TenantDomain() { }

        /// <summary>
        /// Factory: create a new TenantDomain record when initiating a registration.
        /// Status starts as Pending — moves to Active after registrar confirms.
        /// TenantId is always Guid.Empty (platform sentinel) — this entity is NOT tenant-scoped.
        /// </summary>
        public TenantDomain(
            string domain,
            Guid ownerTenantId,
            string registrantEmail,
            RegistrarProvider registrar = RegistrarProvider.GoDaddy,
            DateTime? expiresAt = null)
            : base(new TenantId(Guid.Empty)) // platform-level entity, not tenant-scoped
        {
            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Domain cannot be empty.", nameof(domain));
            if (ownerTenantId == Guid.Empty)
                throw new ArgumentException("OwnerTenantId cannot be Guid.Empty.", nameof(ownerTenantId));
            if (string.IsNullOrWhiteSpace(registrantEmail))
                throw new ArgumentException("RegistrantEmail cannot be empty.", nameof(registrantEmail));

            Domain = CanonicalizeDomain(domain);
            OwnerTenantId = ownerTenantId;
            RegistrantEmail = registrantEmail.Trim().ToLowerInvariant();
            Registrar = registrar;
            RegisteredAt = DateTime.UtcNow;
            ExpiresAt = expiresAt ?? RegisteredAt.AddYears(1);
            AutoRenew = true;
            Status = DomainStatus.Pending;
        }

        /// <summary>
        /// Canonicalize domain input: strip scheme, path, port, trailing slash, lowercase.
        /// Mirrors KhachLinkInstance.CanonicalizeDomain logic.
        /// </summary>
        private static string CanonicalizeDomain(string input)
        {
            var trimmed = input.Trim().ToLowerInvariant();

            if (trimmed.StartsWith("https://"))
                trimmed = trimmed["https://".Length..];
            else if (trimmed.StartsWith("http://"))
                trimmed = trimmed["http://".Length..];

            if (!trimmed.Contains("://"))
                trimmed = "https://" + trimmed;

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
                return uri.Host;

            var slashIdx = trimmed.IndexOf('/');
            if (slashIdx > 0)
                trimmed = trimmed[..slashIdx];

            return trimmed.TrimEnd('/');
        }

        /// <summary>Mark registration as completed — set expiry + clear pending state.</summary>
        public void MarkRegistered(DateTime expiresAt, string? operationId = null)
        {
            if (expiresAt < RegisteredAt)
                throw new ArgumentException("ExpiresAt cannot be before RegisteredAt.", nameof(expiresAt));

            ExpiresAt = expiresAt;
            Status = DomainStatus.Active;
            LastOperationId = operationId;
            LastError = null;
            UpdateAudit();
        }

        /// <summary>Mark registration as failed — record error message for admin review.</summary>
        public void MarkFailed(string errorMessage, string? operationId = null)
        {
            Status = DomainStatus.Failed;
            LastError = errorMessage ?? "Unknown error";
            LastOperationId = operationId;
            UpdateAudit();
        }

        /// <summary>Link this domain to a KhachLinkInstance for custom-domain routing.</summary>
        public void LinkToKhachLinkInstance(Guid khachLinkInstanceId)
        {
            if (khachLinkInstanceId == Guid.Empty)
                throw new ArgumentException("KhachLinkInstanceId cannot be Guid.Empty.", nameof(khachLinkInstanceId));

            KhachLinkInstanceId = khachLinkInstanceId;
            UpdateAudit();
        }

        /// <summary>Unlink from KhachLinkInstance (e.g. when instance is deactivated).</summary>
        public void UnlinkFromKhachLinkInstance()
        {
            KhachLinkInstanceId = null;
            UpdateAudit();
        }

        /// <summary>Renew domain — extend expiry + update status.</summary>
        public void Renew(DateTime newExpiresAt)
        {
            if (newExpiresAt <= ExpiresAt)
                throw new ArgumentException("New ExpiresAt must be after current ExpiresAt.", nameof(newExpiresAt));

            ExpiresAt = newExpiresAt;
            Status = DomainStatus.Active;
            LastError = null;
            UpdateAudit();
        }

        /// <summary>Mark domain as expired — called by expiry-check cron.</summary>
        public void MarkExpired()
        {
            Status = DomainStatus.Expired;
            UpdateAudit();
        }

        /// <summary>Suspend domain — registrar lock, abuse, or admin action.</summary>
        public void Suspend(string reason)
        {
            Status = DomainStatus.Suspended;
            LastError = reason;
            UpdateAudit();
        }

        /// <summary>Reactivate a suspended domain.</summary>
        public void Reactivate()
        {
            Status = DomainStatus.Active;
            LastError = null;
            UpdateAudit();
        }

        /// <summary>Toggle auto-renew flag.</summary>
        public void SetAutoRenew(bool enabled)
        {
            AutoRenew = enabled;
            UpdateAudit();
        }

        /// <summary>Update last operation ID (for async registrar polling).</summary>
        public void SetLastOperationId(string? operationId)
        {
            LastOperationId = operationId;
            UpdateAudit();
        }

        /// <summary>Mark domain as transferred away from Vạn An registrar.</summary>
        public void MarkTransferredAway()
        {
            Status = DomainStatus.TransferredAway;
            KhachLinkInstanceId = null;
            UpdateAudit();
        }
    }
}
