using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.DomainRegistrar;
using VanAn.Shared.Domain.Aggregates.DomainResellerAggregate;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Domain Reseller R1: Admin API for managing tenant-owned domains.
    /// SystemAdmin Bearer JWT for all endpoints.
    ///
    /// Endpoints:
    /// - GET    /api/v1/domains                     — list all TenantDomains
    /// - GET    /api/v1/domains/{id}                — get by Id
    /// - GET    /api/v1/domains/by-domain/{domain}  — get by domain name
    /// - POST   /api/v1/domains                     — create TenantDomain record (after manual registration)
    /// - POST   /api/v1/domains/{id}/link-kli       — link to KhachLinkInstance + auto A record
    /// - POST   /api/v1/domains/{id}/unlink-kli     — unlink from KhachLinkInstance
    /// - POST   /api/v1/domains/{id}/renew          — mark as renewed (after manual renewal)
    /// - GET    /api/v1/domains/availability?domain=— check domain availability + price (registrar API)
    /// - GET    /api/v1/domains/{domain}/dns-records— list DNS records (registrar API)
    /// - PUT    /api/v1/domains/{domain}/a-record   — set A record (registrar API)
    /// - DELETE /api/v1/domains/{domain}/a-record?name=— delete A record (registrar API)
    /// - GET    /api/v1/domains/health              — registrar API health check
    /// </summary>
    [ApiController]
    [Route("api/v1/domains")]
    public class DomainRegistrarController(
        ITenantDomainService tenantDomainService,
        IDomainRegistrarService registrarService,
        ILogger<DomainRegistrarController> logger) : ControllerBase
    {
        private readonly ITenantDomainService _tenantDomainService = tenantDomainService;
        private readonly IDomainRegistrarService _registrarService = registrarService;
        private readonly ILogger<DomainRegistrarController> _logger = logger;

        // ── TenantDomain CRUD (database) ──────────────────────────────

        /// <summary>List all TenantDomains.</summary>
        [HttpGet]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<List<TenantDomainDto>>> List(CancellationToken ct = default)
        {
            var domains = await _tenantDomainService.GetAllAsync(ct);
            return Ok(domains.Select(ToDto).ToList());
        }

        /// <summary>Get TenantDomain by Id.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<TenantDomainDto>> GetById(Guid id, CancellationToken ct = default)
        {
            var domain = await _tenantDomainService.GetByIdAsync(id, ct);
            if (domain is null)
                return NotFound();
            return Ok(ToDto(domain));
        }

        /// <summary>Get TenantDomain by domain name.</summary>
        [HttpGet("by-domain/{domain}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<TenantDomainDto>> GetByDomain(string domain, CancellationToken ct = default)
        {
            var tenantDomain = await _tenantDomainService.GetByDomainAsync(domain, ct);
            if (tenantDomain is null)
                return NotFound();
            return Ok(ToDto(tenantDomain));
        }

        /// <summary>
        /// Create a TenantDomain record. Used after admin manually registers a domain
        /// at the registrar (GoDaddy UI), then links it here for tracking + A record automation.
        /// R2 will add auto-registration via registrar API.
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<TenantDomainDto>> Create(
            [FromBody] CreateTenantDomainRequest request,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var domain = await _tenantDomainService.CreateAsync(
                    request.Domain, request.OwnerTenantId, request.RegistrantEmail,
                    request.Registrar, request.ExpiresAt, ct);
                return CreatedAtAction(nameof(GetById), new { id = domain.Id }, ToDto(domain));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Link a TenantDomain to a KhachLinkInstance + auto-create A record at the registrar.
        /// This is the integration point between Domain Reseller and KhachLink Multi-Profile.
        /// After linking, the domain's apex A record points to the VPS IP, enabling KhachLink
        /// to serve the tenant's storefront on the custom domain.
        /// </summary>
        [HttpPost("{id:guid}/link-kli")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> LinkToKhachLinkInstance(
            Guid id,
            [FromBody] LinkKliRequest request,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var success = await _tenantDomainService.LinkToKhachLinkInstanceAsync(
                id, request.KhachLinkInstanceId, request.VpsIpAddress, ct);
            if (!success)
                return NotFound();
            return NoContent();
        }

        /// <summary>Unlink a TenantDomain from its KhachLinkInstance.</summary>
        [HttpPost("{id:guid}/unlink-kli")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UnlinkFromKhachLinkInstance(Guid id, CancellationToken ct = default)
        {
            var success = await _tenantDomainService.UnlinkFromKhachLinkInstanceAsync(id, ct);
            if (!success)
                return NotFound();
            return NoContent();
        }

        /// <summary>Mark a TenantDomain as renewed (after manual renewal at registrar UI).</summary>
        [HttpPost("{id:guid}/renew")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Renew(Guid id, [FromBody] RenewDomainRequest request, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var success = await _tenantDomainService.RenewAsync(id, request.NewExpiresAt, ct);
            if (!success)
                return NotFound();
            return NoContent();
        }

        // ── Registrar API passthrough (GoDaddy) ─────────────────────────

        /// <summary>Check domain availability + pricing at registrar (GoDaddy API).</summary>
        [HttpGet("availability")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<DomainAvailabilityResult>> CheckAvailability(
            [FromQuery] string domain, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return BadRequest(new { error = "Domain query parameter is required." });

            var result = await _registrarService.CheckAvailabilityAsync(domain, ct);
            return Ok(result);
        }

        /// <summary>List all DNS records for a domain (registrar API).</summary>
        [HttpGet("{domain}/dns-records")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<List<DnsRecordDto>>> GetDnsRecords(string domain, CancellationToken ct = default)
        {
            var records = await _registrarService.GetDnsRecordsAsync(domain, ct);
            return Ok(records);
        }

        /// <summary>Set A record on a domain (registrar API). Creates or replaces all A records for the given name.</summary>
        [HttpPut("{domain}/a-record")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> SetARecord(string domain, [FromBody] SetARecordRequest request, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var success = await _registrarService.SetARecordAsync(
                domain, request.Name ?? "@", request.IpAddress, request.Ttl ?? 600, ct);
            if (!success)
                return StatusCode(502, new { error = "Failed to set A record at registrar." });
            return NoContent();
        }

        /// <summary>Delete all A records for a name on a domain (registrar API).</summary>
        [HttpDelete("{domain}/a-record")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeleteARecord(string domain, [FromQuery] string? name, CancellationToken ct = default)
        {
            var success = await _registrarService.DeleteARecordAsync(domain, name ?? "@", ct);
            if (!success)
                return StatusCode(502, new { error = "Failed to delete A record at registrar." });
            return NoContent();
        }

        /// <summary>Registrar API health check — verify credentials work.</summary>
        [HttpGet("health")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<object>> HealthCheck(CancellationToken ct = default)
        {
            var healthy = await _registrarService.HealthCheckAsync(ct);
            return Ok(new
            {
                healthy,
                provider = _registrarService.Provider.ToString(),
                timestamp = DateTime.UtcNow
            });
        }

        // ── DTOs ────────────────────────────────────────────────────────

        private static TenantDomainDto ToDto(TenantDomain d) => new()
        {
            Id = d.Id,
            Domain = d.Domain,
            Registrar = d.Registrar,
            OwnerTenantId = d.OwnerTenantId,
            KhachLinkInstanceId = d.KhachLinkInstanceId,
            RegisteredAt = d.RegisteredAt,
            ExpiresAt = d.ExpiresAt,
            AutoRenew = d.AutoRenew,
            Status = d.Status,
            RegistrantEmail = d.RegistrantEmail,
            LastOperationId = d.LastOperationId,
            LastError = d.LastError,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt
        };
    }

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
        /// <summary>VPS IP address to point the domain's A record to.</summary>
        public string VpsIpAddress { get; set; } = string.Empty;
    }

    public sealed class RenewDomainRequest
    {
        public DateTime NewExpiresAt { get; set; }
    }

    public sealed class SetARecordRequest
    {
        /// <summary>Record name — "@" for apex, "www" for subdomain. Default "@".</summary>
        public string? Name { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public int? Ttl { get; set; }
    }
}
