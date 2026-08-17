using VanAn.Shared.Domain.Aggregates.DomainResellerAggregate;

namespace VanAn.CoreHub.Services.DomainRegistrar
{
    /// <summary>
    /// Abstraction over domain registrar APIs (GoDaddy, Namecheap, P.A Vietnam).
    /// Each provider implements this interface; FailoverRegistrarService wraps multiple
    /// implementations with primary/backup failover logic.
    ///
    /// All operations are async and cancellable. Write operations (Register, SetARecord,
    /// DeleteRecord, Renew) may incur real charges at the registrar — callers must
    /// confirm intent before calling.
    ///
    /// Domain Reseller R1: GoDaddy implementation verified 2026-08-17 (read + write + delete).
    /// </summary>
    public interface IDomainRegistrarService
    {
        /// <summary>Provider identifier — drives routing in FailoverRegistrarService.</summary>
        RegistrarProvider Provider { get; }

        /// <summary>Check if a domain is available for registration. Returns price in micro-units (1 USD = 10,000,000).</summary>
        Task<DomainAvailabilityResult> CheckAvailabilityAsync(string domain, CancellationToken ct = default);

        /// <summary>Register a new domain. Charges the registrar account. Returns registration result with expiry date.</summary>
        Task<DomainRegistrationResult> RegisterAsync(string domain, int years, string registrantEmail, CancellationToken ct = default);

        /// <summary>Renew an existing domain. Charges the registrar account. Returns new expiry date.</summary>
        Task<DomainRenewalResult> RenewAsync(string domain, int years, CancellationToken ct = default);

        /// <summary>Set an A record on a domain (creates or replaces all A records for the given name).</summary>
        Task<bool> SetARecordAsync(string domain, string name, string ipAddress, int ttl = 600, CancellationToken ct = default);

        /// <summary>Delete all A records for a given name on a domain.</summary>
        Task<bool> DeleteARecordAsync(string domain, string name, CancellationToken ct = default);

        /// <summary>List all DNS records for a domain (all types).</summary>
        Task<List<DnsRecordDto>> GetDnsRecordsAsync(string domain, CancellationToken ct = default);

        /// <summary>List all A records for a specific name on a domain.</summary>
        Task<List<DnsRecordDto>> GetARecordsAsync(string domain, string name, CancellationToken ct = default);

        /// <summary>Health check — verify API credentials work. Used by FailoverRegistrarService polling.</summary>
        Task<bool> HealthCheckAsync(CancellationToken ct = default);
    }

    /// <summary>Result of domain availability check.</summary>
    public sealed record DomainAvailabilityResult
    {
        public required string Domain { get; init; }
        public required bool Available { get; init; }
        /// <summary>Price in micro-units (1 USD = 10,000,000). Null if unavailable.</summary>
        public long? PriceMicroUnits { get; init; }
        /// <summary>Renewal price in micro-units. Null if unavailable or not provided.</summary>
        public long? RenewalPriceMicroUnits { get; init; }
        public string? Currency { get; init; }
        public string? Error { get; init; }
    }

    /// <summary>Result of domain registration.</summary>
    public sealed record DomainRegistrationResult
    {
        public required string Domain { get; init; }
        public required bool Success { get; init; }
        public DateTime? ExpiresAt { get; init; }
        public string? OperationId { get; init; }
        public string? Error { get; init; }
    }

    /// <summary>Result of domain renewal.</summary>
    public sealed record DomainRenewalResult
    {
        public required string Domain { get; init; }
        public required bool Success { get; init; }
        public DateTime? NewExpiresAt { get; init; }
        public string? Error { get; init; }
    }

    /// <summary>Generic DNS record DTO (provider-agnostic).</summary>
    public sealed record DnsRecordDto
    {
        public required string Type { get; init; }
        public required string Name { get; init; }
        public required string Data { get; init; }
        public int Ttl { get; init; }
        public int? MxPreference { get; init; }
    }
}
