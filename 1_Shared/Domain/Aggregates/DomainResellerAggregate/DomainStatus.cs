namespace VanAn.Shared.Domain.Aggregates.DomainResellerAggregate
{
    /// <summary>
    /// Lifecycle status of a tenant-owned domain managed via Vạn An reseller platform.
    /// Drives admin UI badges + auto-renew + expiry alert cron logic.
    /// </summary>
    public enum DomainStatus
    {
        /// <summary>Domain registered and active — DNS serving, SSL provisioned.</summary>
        Active = 0,

        /// <summary>Registration pending — async registrar operation in progress (GoDaddy v3 quote-execute model).</summary>
        Pending = 1,

        /// <summary>Domain expired — not renewed before expiry. KhachLink instance should be disabled.</summary>
        Expired = 2,

        /// <summary>Domain suspended — registrar lock, abuse, or manual admin action.</summary>
        Suspended = 3,

        /// <summary>Registration failed — registrar returned error, no domain created.</summary>
        Failed = 4,

        /// <summary>Domain transferred away to another registrar (no longer managed by Vạn An).</summary>
        TransferredAway = 5
    }
}
