namespace VanAn.Shared.Domain.Aggregates.DomainResellerAggregate
{
    /// <summary>
    /// Domain registrar provider — identifies which registrar manages a given TenantDomain.
    /// Used by FailoverRegistrarService to route operations to the correct API client.
    /// </summary>
    public enum RegistrarProvider
    {
        /// <summary>GoDaddy API v1 — primary registrar (verified 2026-08-17).</summary>
        GoDaddy = 0,

        /// <summary>Namecheap API — backup registrar (R2, pending sandbox verify).</summary>
        Namecheap = 1,

        /// <summary>P.A Vietnam API — Vietnam-focused registrar (.VN + VND support, R3).</summary>
        PaVietnam = 2
    }
}
