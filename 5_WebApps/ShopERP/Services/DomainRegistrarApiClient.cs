using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain.Aggregates.DomainResellerAggregate;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// ShopERP client for the Gateway DomainRegistrar admin API.
    /// Calls /api/v1/domains with SystemAdmin Bearer JWT.
    /// Uses PostgreSQL (Gateway DB) — NOT ShopERP SQLite.
    /// Pattern follows KhachLinkInstanceApiClient.
    /// </summary>
    public sealed class DomainRegistrarApiClient : GatewayAdminApiClientBase
    {
        public DomainRegistrarApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<DomainRegistrarApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        // ── TenantDomain CRUD ──────────────────────────────────────────

        public async Task<List<TenantDomainDto>> ListAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/v1/domains");
            return await SendAndReadAsync<List<TenantDomainDto>>(HttpClient, req, ct) ?? new();
        }

        public async Task<TenantDomainDto> CreateAsync(CreateTenantDomainRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, "api/v1/domains", request);
            return await SendAndReadAsync<TenantDomainDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned an empty TenantDomain response.");
        }

        public async Task LinkKliAsync(Guid id, LinkKliRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/v1/domains/{id}/link-kli", request);
            var response = await HttpClient.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
        }

        public async Task UnlinkKliAsync(Guid id, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/v1/domains/{id}/unlink-kli");
            var response = await HttpClient.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
        }

        public async Task RenewAsync(Guid id, RenewDomainRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/v1/domains/{id}/renew", request);
            var response = await HttpClient.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
        }

        // ── Registrar API passthrough ──────────────────────────────────

        public async Task<DomainAvailabilityResultDto> CheckAvailabilityAsync(string domain, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/v1/domains/availability?domain={Uri.EscapeDataString(domain)}");
            return await SendAndReadAsync<DomainAvailabilityResultDto>(HttpClient, req, ct) ?? new();
        }

        public async Task<List<DnsRecordDto>> GetDnsRecordsAsync(string domain, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/v1/domains/{Uri.EscapeDataString(domain)}/dns-records");
            return await SendAndReadAsync<List<DnsRecordDto>>(HttpClient, req, ct) ?? new();
        }

        public async Task<bool> SetARecordAsync(string domain, SetARecordRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Put, $"api/v1/domains/{Uri.EscapeDataString(domain)}/a-record", request);
            var response = await HttpClient.SendAsync(req, ct);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteARecordAsync(string domain, string? name, CancellationToken ct = default)
        {
            var url = $"api/v1/domains/{Uri.EscapeDataString(domain)}/a-record";
            if (!string.IsNullOrEmpty(name))
                url += $"?name={Uri.EscapeDataString(name)}";
            var req = await CreateRequestAsync(HttpMethod.Delete, url);
            var response = await HttpClient.SendAsync(req, ct);
            return response.IsSuccessStatusCode;
        }

        public async Task<RegistrarHealthDto> HealthCheckAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/v1/domains/health");
            return await SendAndReadAsync<RegistrarHealthDto>(HttpClient, req, ct) ?? new();
        }
    }

    // ── DTOs (mirror Gateway DomainRegistrarController DTOs) ───────────

    public sealed class TenantDomainDto
    {
        public Guid Id { get; set; }
        public string Domain { get; set; } = string.Empty;
        public RegistrarProvider Registrar { get; set; }
        public Guid OwnerTenantId { get; set; }
        public Guid? KhachLinkInstanceId { get; set; }
        public DateTime RegisteredAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool AutoRenew { get; set; }
        public DomainStatus Status { get; set; }
        public string RegistrantEmail { get; set; } = string.Empty;
        public string? LastOperationId { get; set; }
        public string? LastError { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class CreateTenantDomainRequest
    {
        public string Domain { get; set; } = string.Empty;
        public Guid OwnerTenantId { get; set; }
        public string RegistrantEmail { get; set; } = string.Empty;
        public RegistrarProvider Registrar { get; set; } = RegistrarProvider.GoDaddy;
        public DateTime? ExpiresAt { get; set; }
    }

    public sealed class LinkKliRequest
    {
        public Guid KhachLinkInstanceId { get; set; }
        public string VpsIpAddress { get; set; } = string.Empty;
    }

    public sealed class RenewDomainRequest
    {
        public DateTime NewExpiresAt { get; set; }
    }

    public sealed class SetARecordRequest
    {
        public string? Name { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public int? Ttl { get; set; }
    }

    public sealed class DomainAvailabilityResultDto
    {
        public string Domain { get; set; } = string.Empty;
        public bool Available { get; set; }
        public long? PriceMicroUnits { get; set; }
        public long? RenewalPriceMicroUnits { get; set; }
        public string? Currency { get; set; }
        public string? Error { get; set; }
    }

    public sealed class DnsRecordDto
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public int Ttl { get; set; }
        public int? MxPreference { get; set; }
    }

    public sealed class RegistrarHealthDto
    {
        public bool Healthy { get; set; }
        public string Provider { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
