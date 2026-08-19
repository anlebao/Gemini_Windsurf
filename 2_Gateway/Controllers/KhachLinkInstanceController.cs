using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// KhachLink Multi-Profile R1: Admin API for managing KhachLink instances.
    /// Platform-level CRUD + by-domain public lookup (for KhachLink runtime).
    /// SystemAdmin Bearer JWT for CRUD; anonymous for by-domain lookup.
    /// </summary>
    [ApiController]
    [Route("api/v1/khachlink-instances")]
    public class KhachLinkInstanceController(
        IKhachLinkInstanceService instanceService,
        IConfiguration configuration,
        ILogger<KhachLinkInstanceController> logger,
        ITenantDomainService? tenantDomainService = null) : ControllerBase
    {
        private readonly IKhachLinkInstanceService _instanceService = instanceService;
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<KhachLinkInstanceController> _logger = logger;
        private readonly ITenantDomainService? _tenantDomainService = tenantDomainService;

        /// <summary>
        /// Feature flag check — if KhachLink:MultiProfileEnabled is false,
        /// by-domain endpoint returns 404 (KhachLink client falls back to FullCommerce default).
        /// </summary>
        private bool IsMultiProfileEnabled()
            => _configuration.GetValue<bool>("KhachLink:MultiProfileEnabled", false);

        /// <summary>List all KhachLinkInstances.</summary>
        [HttpGet]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<List<KhachLinkInstanceDto>>> List(CancellationToken ct = default)
        {
            var instances = await _instanceService.GetAllAsync(ct);
            return Ok(instances.Select(ToDto).ToList());
        }

        /// <summary>Get a KhachLinkInstance by Id.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<KhachLinkInstanceDto>> GetById(Guid id, CancellationToken ct = default)
        {
            var instance = await _instanceService.GetByIdAsync(id, ct);
            if (instance is null)
                return NotFound();
            return Ok(ToDto(instance));
        }

        /// <summary>
        /// Public lookup by custom domain — used by KhachLink runtime on page load.
        /// Anonymous (no auth) — returns only active instances.
        /// Returns 404 if feature flag OFF or domain not found.
        /// </summary>
        [HttpGet("by-domain/{domain}")]
        [AllowAnonymous]
        public async Task<ActionResult<KhachLinkInstanceDto>> GetByDomain(string domain, CancellationToken ct = default)
        {
            if (!IsMultiProfileEnabled())
            {
                _logger.LogDebug("GetByDomain: feature flag OFF, returning 404 for {Domain}", domain);
                return NotFound();
            }

            var instance = await _instanceService.GetByDomainAsync(domain, ct);
            if (instance is null)
                return NotFound();

            return Ok(ToDto(instance));
        }

        /// <summary>Create a new KhachLinkInstance.</summary>
        [HttpPost]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<KhachLinkInstanceDto>> Create(
            [FromBody] CreateKhachLinkInstanceRequest request,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                KhachLinkNavFlags? navFlagsOverride = null;
                if (request.NavFlagsOverride != null)
                {
                    navFlagsOverride = new KhachLinkNavFlags
                    {
                        ShowHome = request.NavFlagsOverride.ShowHome,
                        ShowCart = request.NavFlagsOverride.ShowCart,
                        ShowOrders = request.NavFlagsOverride.ShowOrders,
                        ShowLoyaltyHistory = request.NavFlagsOverride.ShowLoyaltyHistory,
                        ShowMissions = request.NavFlagsOverride.ShowMissions,
                        ShowRewards = request.NavFlagsOverride.ShowRewards,
                        ShowAllianceWallet = request.NavFlagsOverride.ShowAllianceWallet,
                        ShowStores = request.NavFlagsOverride.ShowStores,
                        ShowCampaigns = request.NavFlagsOverride.ShowCampaigns,
                        ShowScan = request.NavFlagsOverride.ShowScan,
                        ShowQrClaim = request.NavFlagsOverride.ShowQrClaim,
                        ShowCommunity = request.NavFlagsOverride.ShowCommunity,
                        ShowJobs = request.NavFlagsOverride.ShowJobs,
                        ShowProfile = request.NavFlagsOverride.ShowProfile,
                        ShowStaffDashboard = request.NavFlagsOverride.ShowStaffDashboard
                    };
                }

                var instance = await _instanceService.CreateAsync(
                    request.Label, request.Profile, request.CustomDomain,
                    request.OwnerTenantId, navFlagsOverride, ct);

                // Domain Reseller R1: Auto-link to matching TenantDomain if exists.
                // When admin creates a KhachLinkInstance with a custom domain that matches
                // a TenantDomain record, automatically link them + create A record at registrar.
                // Best-effort — failures are logged but don't block instance creation.
                if (_tenantDomainService != null && !string.IsNullOrWhiteSpace(instance.CustomDomain))
                {
                    try
                    {
                        var tenantDomain = await _tenantDomainService.GetByDomainAsync(instance.CustomDomain, ct);
                        if (tenantDomain != null && tenantDomain.KhachLinkInstanceId == null)
                        {
                            var vpsIp = _configuration["DomainRegistrar:DefaultVpsIp"] ?? "";
                            if (!string.IsNullOrEmpty(vpsIp))
                            {
                                await _tenantDomainService.LinkToKhachLinkInstanceAsync(
                                    tenantDomain.Id, instance.Id, vpsIp, ct);
                                _logger.LogInformation("Auto-linked TenantDomain {Domain} → KhachLinkInstance {KliId}",
                                    tenantDomain.Domain, instance.Id);
                            }
                        }
                    }
                    catch (Exception autoLinkEx)
                    {
                        _logger.LogWarning(autoLinkEx, "Auto-link TenantDomain failed for {Domain} — instance created, manual link needed",
                            instance.CustomDomain);
                    }
                }

                return CreatedAtAction(nameof(GetById), new { id = instance.Id }, ToDto(instance));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid KhachLinkInstance create request");
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "KhachLinkInstance conflict");
                return Conflict(new { error = ex.Message });
            }
        }

        /// <summary>Update profile + nav flags.</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateKhachLinkInstanceRequest request,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var navFlags = new KhachLinkNavFlags
                {
                    ShowHome = request.NavFlags.ShowHome,
                    ShowCart = request.NavFlags.ShowCart,
                    ShowOrders = request.NavFlags.ShowOrders,
                    ShowLoyaltyHistory = request.NavFlags.ShowLoyaltyHistory,
                    ShowMissions = request.NavFlags.ShowMissions,
                    ShowRewards = request.NavFlags.ShowRewards,
                    ShowAllianceWallet = request.NavFlags.ShowAllianceWallet,
                    ShowStores = request.NavFlags.ShowStores,
                    ShowCampaigns = request.NavFlags.ShowCampaigns,
                    ShowScan = request.NavFlags.ShowScan,
                    ShowQrClaim = request.NavFlags.ShowQrClaim,
                    ShowCommunity = request.NavFlags.ShowCommunity,
                    ShowJobs = request.NavFlags.ShowJobs,
                    ShowProfile = request.NavFlags.ShowProfile,
                    ShowStaffDashboard = request.NavFlags.ShowStaffDashboard
                };

                var updated = await _instanceService.UpdateAsync(
                    id, request.Profile, navFlags,
                    request.Theme, request.LogoUrl, request.NavColor, request.HeaderColor, request.FooterColor,
                    ct);
                if (!updated)
                    return NotFound();
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Deactivate a KhachLinkInstance (soft delete).</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct = default)
        {
            var deactivated = await _instanceService.DeactivateAsync(id, ct);
            if (!deactivated)
                return NotFound();
            return NoContent();
        }

        /// <summary>#134: Activate a previously deactivated KhachLinkInstance.</summary>
        [HttpPost("{id:guid}/activate")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Activate(Guid id, CancellationToken ct = default)
        {
            var activated = await _instanceService.ActivateAsync(id, ct);
            if (!activated)
                return NotFound();
            return NoContent();
        }

        /// <summary>Map entity to DTO.</summary>
        private static KhachLinkInstanceDto ToDto(KhachLinkInstance i) => new()
        {
            Id = i.Id,
            Label = i.Label,
            Profile = i.Profile,
            CustomDomain = i.CustomDomain,
            OwnerTenantId = i.OwnerTenantId,
            IsActive = i.IsActive,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt,
            // Issue #143: style override fields
            Theme = i.Theme,
            LogoUrl = i.LogoUrl,
            NavColor = i.NavColor,
            HeaderColor = i.HeaderColor,
            FooterColor = i.FooterColor,
            NavFlags = new KhachLinkNavFlagsDto
            {
                ShowHome = i.NavFlags.ShowHome,
                ShowCart = i.NavFlags.ShowCart,
                ShowOrders = i.NavFlags.ShowOrders,
                ShowLoyaltyHistory = i.NavFlags.ShowLoyaltyHistory,
                ShowMissions = i.NavFlags.ShowMissions,
                ShowRewards = i.NavFlags.ShowRewards,
                ShowAllianceWallet = i.NavFlags.ShowAllianceWallet,
                ShowStores = i.NavFlags.ShowStores,
                ShowCampaigns = i.NavFlags.ShowCampaigns,
                ShowScan = i.NavFlags.ShowScan,
                ShowQrClaim = i.NavFlags.ShowQrClaim,
                ShowCommunity = i.NavFlags.ShowCommunity,
                ShowJobs = i.NavFlags.ShowJobs,
                ShowProfile = i.NavFlags.ShowProfile,
                ShowStaffDashboard = i.NavFlags.ShowStaffDashboard
            }
        };
    }

    // ── DTOs ──────────────────────────────────────────────────────────────

    public sealed class KhachLinkInstanceDto
    {
        public Guid Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public KhachLinkProfile Profile { get; set; }
        public string CustomDomain { get; set; } = string.Empty;
        public Guid? OwnerTenantId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public KhachLinkNavFlagsDto NavFlags { get; set; } = new();
        // Issue #143: style override fields (null = inherit from tenant ShopConfig)
        public string? Theme { get; set; }
        public string? LogoUrl { get; set; }
        public string? NavColor { get; set; }
        public string? HeaderColor { get; set; }
        public string? FooterColor { get; set; }
    }

    public sealed class KhachLinkNavFlagsDto
    {
        public bool ShowHome { get; set; } = true;
        public bool ShowCart { get; set; } = true;
        public bool ShowOrders { get; set; } = true;
        public bool ShowLoyaltyHistory { get; set; } = true;
        public bool ShowMissions { get; set; } = true;
        public bool ShowRewards { get; set; } = true;
        public bool ShowAllianceWallet { get; set; } = true;
        public bool ShowStores { get; set; } = true;
        public bool ShowCampaigns { get; set; } = true;
        public bool ShowScan { get; set; } = true;
        public bool ShowQrClaim { get; set; } = true;
        public bool ShowCommunity { get; set; } = true;
        public bool ShowJobs { get; set; } = false;
        public bool ShowProfile { get; set; } = true;
        public bool ShowStaffDashboard { get; set; } = true;
    }

    public sealed class CreateKhachLinkInstanceRequest
    {
        public string Label { get; set; } = string.Empty;
        public KhachLinkProfile Profile { get; set; } = KhachLinkProfile.FullCommerce;
        public string CustomDomain { get; set; } = string.Empty;
        public Guid? OwnerTenantId { get; set; }
        public KhachLinkNavFlagsDto? NavFlagsOverride { get; set; }
    }

    public sealed class UpdateKhachLinkInstanceRequest
    {
        public KhachLinkProfile Profile { get; set; } = KhachLinkProfile.FullCommerce;
        public KhachLinkNavFlagsDto NavFlags { get; set; } = new();
        // Issue #143: style override fields (null/empty = clear override, inherit from tenant ShopConfig)
        public string? Theme { get; set; }
        public string? LogoUrl { get; set; }
        public string? NavColor { get; set; }
        public string? HeaderColor { get; set; }
        public string? FooterColor { get; set; }
    }
}
