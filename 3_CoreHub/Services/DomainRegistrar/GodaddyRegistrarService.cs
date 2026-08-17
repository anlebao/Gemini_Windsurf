using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain.Aggregates.DomainResellerAggregate;

namespace VanAn.CoreHub.Services.DomainRegistrar
{
    /// <summary>
    /// GoDaddy API v1 implementation of IDomainRegistrarService.
    /// Verified 2026-08-17: read (list domains, get records, availability) + write (add A record, delete A record) all PASS.
    ///
    /// API reference:
    /// - Base URL: https://api.godaddy.com (production)
    /// - Auth: Authorization: Bearer {PAT} (Personal Access Token)
    /// - DNS records: GET/PUT/DELETE /v1/domains/{domain}/records/{type}/{name}
    /// - Availability: GET /v1/domains/available?domain={domain}
    /// - Domain list: GET /v1/domains?statuses=ACTIVE
    ///
    /// Configuration (appsettings.json or env vars):
    /// - "DomainRegistrar:GoDaddy:ApiKey" — PAT (Personal Access Token)
    /// - "DomainRegistrar:GoDaddy:ApiUrl" — base URL (default: https://api.godaddy.com)
    ///
    /// Note: v1 API uses sso-key auth (key:secret) for legacy keys, but PAT uses Bearer.
    /// This implementation uses PAT (Bearer) — v3 registration requires PAT, v1 DNS works with PAT too.
    /// </summary>
    public class GodaddyRegistrarService : IDomainRegistrarService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GodaddyRegistrarService>? _logger;

        public RegistrarProvider Provider => RegistrarProvider.GoDaddy;

        public GodaddyRegistrarService(
            IConfiguration configuration,
            ILogger<GodaddyRegistrarService>? logger = null)
        {
            // R1-fix: Don't throw in constructor — defer API key check to first actual use.
            // Throwing in constructor prevents DI from creating the entire controller chain
            // (KhachLinkInstanceController → ITenantDomainService → IDomainRegistrarService),
            // which causes ALL KhachLink endpoints to return 400 "Operation is not valid"
            // even when the registrar service is not needed (e.g. by-domain anonymous lookup).
            // This way, the service is created lazily and only fails when a registrar API
            // call is actually made (SetARecord, CheckAvailability, etc.).
            _apiKey = configuration["DomainRegistrar:GoDaddy:ApiKey"];
            _apiUrl = configuration["DomainRegistrar:GoDaddy:ApiUrl"] ?? "https://api.godaddy.com";
            _logger = logger;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_apiUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            // Only set auth header if API key is present — HealthCheck will return false if missing.
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private readonly string? _apiKey;
        private readonly string _apiUrl;

        /// <summary>
        /// Throws if API key is not configured. Called before any registrar API operation.
        /// </summary>
        private void EnsureConfigured()
        {
            if (string.IsNullOrEmpty(_apiKey))
                throw new InvalidOperationException("DomainRegistrar:GoDaddy:ApiKey not configured. Add it to .env.gateway or appsettings.json.");
        }

        public async Task<DomainAvailabilityResult> CheckAvailabilityAsync(string domain, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return new DomainAvailabilityResult { Domain = "", Available = false, Error = "Domain cannot be empty." };

            EnsureConfigured();
            try
            {
                var response = await _httpClient.GetAsync($"/v1/domains/available?domain={Uri.EscapeDataString(domain.ToLowerInvariant())}", ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    _logger?.LogWarning("GoDaddy availability check failed for {Domain}: {Status} {Body}", domain, response.StatusCode, errorBody);
                    return new DomainAvailabilityResult
                    {
                        Domain = domain,
                        Available = false,
                        Error = $"HTTP {(int)response.StatusCode}: {errorBody}"
                    };
                }

                var result = await response.Content.ReadFromJsonAsync<GodaddyAvailabilityResponse>(cancellationToken: ct);
                if (result is null)
                    return new DomainAvailabilityResult { Domain = domain, Available = false, Error = "Empty response body." };

                return new DomainAvailabilityResult
                {
                    Domain = result.Domain ?? domain,
                    Available = result.Available,
                    PriceMicroUnits = result.Price,
                    RenewalPriceMicroUnits = result.RenewalPrice,
                    Currency = result.Currency
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GoDaddy availability check exception for {Domain}", domain);
                return new DomainAvailabilityResult { Domain = domain, Available = false, Error = ex.Message };
            }
        }

        public async Task<DomainRegistrationResult> RegisterAsync(string domain, int years, string registrantEmail, CancellationToken ct = default)
        {
            // GoDaddy v1 purchase endpoint requires full contact info + agreement consent.
            // For R1 MVP, we delegate registration to GoDaddy's v1 purchase API.
            // NOTE: This charges the account. Callers must confirm intent.
            //
            // Full implementation would use v3 quote-execute model:
            //   1. POST /v3/domains/registration-quotes → get quoteToken
            //   2. POST /v3/domains/registrations with Idempotency-Key → 202 Accepted
            //   3. Poll GET /v3/domains/registrations/{registrationId} until COMPLETED
            //
            // For R1 MVP, we return a "not implemented" result — actual registration
            // is done manually by admin via GoDaddy UI, then linked here via the
            // "link existing domain" flow. Full API registration is R2.
            _logger?.LogWarning("GoDaddy RegisterAsync not implemented for R1 — use manual registration + link flow.");
            await Task.CompletedTask;
            return new DomainRegistrationResult
            {
                Domain = domain,
                Success = false,
                Error = "R1: Auto-registration not implemented. Use manual registration via GoDaddy UI + 'Link existing domain' flow in admin UI."
            };
        }

        public async Task<DomainRenewalResult> RenewAsync(string domain, int years, CancellationToken ct = default)
        {
            // GoDaddy v1 renewal: POST /v1/domains/{domain}/renew
            // Similar to RegisterAsync — R1 MVP delegates to manual UI flow.
            _logger?.LogWarning("GoDaddy RenewAsync not implemented for R1 — use manual renewal via GoDaddy UI.");
            await Task.CompletedTask;
            return new DomainRenewalResult
            {
                Domain = domain,
                Success = false,
                Error = "R1: Auto-renewal not implemented. Use manual renewal via GoDaddy UI."
            };
        }

        public async Task<bool> SetARecordAsync(string domain, string name, string ipAddress, int ttl = 600, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Domain cannot be empty.", nameof(domain));

            EnsureConfigured();
            var normalizedDomain = domain.ToLowerInvariant();
            var normalizedName = string.IsNullOrWhiteSpace(name) ? "@" : name.ToLowerInvariant();

            // GoDaddy PUT /v1/domains/{domain}/records/A/{name} replaces ALL records for type+name.
            // Send array with single record to set/replace.
            var records = new[]
            {
                new GodaddyDnsRecord
                {
                    Data = ipAddress,
                    Name = normalizedName,
                    Ttl = ttl,
                    Type = "A"
                }
            };

            try
            {
                var response = await _httpClient.PutAsJsonAsync(
                    $"/v1/domains/{Uri.EscapeDataString(normalizedDomain)}/records/A/{Uri.EscapeDataString(normalizedName)}",
                    records,
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    _logger?.LogError("GoDaddy SetARecord failed for {Domain}/{Name}: {Status} {Body}",
                        normalizedDomain, normalizedName, response.StatusCode, errorBody);
                    return false;
                }

                _logger?.LogInformation("GoDaddy SetARecord success: {Domain}/{Name} → {IP} (TTL {Ttl})",
                    normalizedDomain, normalizedName, ipAddress, ttl);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GoDaddy SetARecord exception for {Domain}/{Name}", normalizedDomain, normalizedName);
                return false;
            }
        }

        public async Task<bool> DeleteARecordAsync(string domain, string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Domain cannot be empty.", nameof(domain));

            EnsureConfigured();
            var normalizedDomain = domain.ToLowerInvariant();
            var normalizedName = string.IsNullOrWhiteSpace(name) ? "@" : name.ToLowerInvariant();

            try
            {
                var response = await _httpClient.DeleteAsync(
                    $"/v1/domains/{Uri.EscapeDataString(normalizedDomain)}/records/A/{Uri.EscapeDataString(normalizedName)}",
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    _logger?.LogError("GoDaddy DeleteARecord failed for {Domain}/{Name}: {Status} {Body}",
                        normalizedDomain, normalizedName, response.StatusCode, errorBody);
                    return false;
                }

                _logger?.LogInformation("GoDaddy DeleteARecord success: {Domain}/{Name}", normalizedDomain, normalizedName);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GoDaddy DeleteARecord exception for {Domain}/{Name}", normalizedDomain, normalizedName);
                return false;
            }
        }

        public async Task<List<DnsRecordDto>> GetDnsRecordsAsync(string domain, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return new List<DnsRecordDto>();

            EnsureConfigured();
            var normalizedDomain = domain.ToLowerInvariant();

            try
            {
                var response = await _httpClient.GetAsync(
                    $"/v1/domains/{Uri.EscapeDataString(normalizedDomain)}/records",
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    _logger?.LogWarning("GoDaddy GetDnsRecords failed for {Domain}: {Status} {Body}",
                        normalizedDomain, response.StatusCode, errorBody);
                    return new List<DnsRecordDto>();
                }

                var records = await response.Content.ReadFromJsonAsync<List<GodaddyDnsRecord>>(cancellationToken: ct);
                if (records is null)
                    return new List<DnsRecordDto>();

                return records.Select(r => new DnsRecordDto
                {
                    Type = r.Type ?? "",
                    Name = r.Name ?? "",
                    Data = r.Data ?? "",
                    Ttl = r.Ttl,
                    MxPreference = r.MxPreference
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GoDaddy GetDnsRecords exception for {Domain}", normalizedDomain);
                return new List<DnsRecordDto>();
            }
        }

        public async Task<List<DnsRecordDto>> GetARecordsAsync(string domain, string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return new List<DnsRecordDto>();

            EnsureConfigured();
            var normalizedDomain = domain.ToLowerInvariant();
            var normalizedName = string.IsNullOrWhiteSpace(name) ? "@" : name.ToLowerInvariant();

            try
            {
                var response = await _httpClient.GetAsync(
                    $"/v1/domains/{Uri.EscapeDataString(normalizedDomain)}/records/A/{Uri.EscapeDataString(normalizedName)}",
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    _logger?.LogWarning("GoDaddy GetARecords failed for {Domain}/{Name}: {Status} {Body}",
                        normalizedDomain, normalizedName, response.StatusCode, errorBody);
                    return new List<DnsRecordDto>();
                }

                var records = await response.Content.ReadFromJsonAsync<List<GodaddyDnsRecord>>(cancellationToken: ct);
                if (records is null)
                    return new List<DnsRecordDto>();

                return records.Select(r => new DnsRecordDto
                {
                    Type = r.Type ?? "A",
                    Name = r.Name ?? normalizedName,
                    Data = r.Data ?? "",
                    Ttl = r.Ttl,
                    MxPreference = r.MxPreference
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GoDaddy GetARecords exception for {Domain}/{Name}", normalizedDomain, normalizedName);
                return new List<DnsRecordDto>();
            }
        }

        public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_apiKey))
                return false;
            try
            {
                // List active domains — if 200, credentials work.
                var response = await _httpClient.GetAsync("/v1/domains?statuses=ACTIVE&limit=1", ct);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GoDaddy HealthCheck exception");
                return false;
            }
        }

        // ── GoDaddy API response models (JSON) ──────────────────────────

        private sealed class GodaddyAvailabilityResponse
        {
            public string? Domain { get; set; }
            public bool Available { get; set; }
            public long? Price { get; set; }
            public long? RenewalPrice { get; set; }
            public string? Currency { get; set; }
            public bool Definitive { get; set; }
            public int Period { get; set; }
        }

        private sealed class GodaddyDnsRecord
        {
            public string? Data { get; set; }
            public string? Name { get; set; }
            public int Ttl { get; set; }
            public string? Type { get; set; }
            public int? MxPreference { get; set; }
        }
    }
}
