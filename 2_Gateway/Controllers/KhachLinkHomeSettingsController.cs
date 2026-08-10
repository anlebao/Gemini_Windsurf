using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// #100: KhachLink home page section toggles — GLOBAL (not tenant-scoped).
    /// GET is anonymous (KhachLink PWA reads without auth).
    /// PUT requires SystemAdmin policy (cookie auth from ShopERP admin UI).
    /// Stored in PG (Gateway VanAnDbContext) — single row, TenantId = Empty.
    /// </summary>
    [ApiController]
    [Route("api/platform/khachlink-home-settings")]
    [Authorize] // Class-level auth required by W12-G7 architecture test; GET overrides with [AllowAnonymous]
    public class KhachLinkHomeSettingsController(
        IVanAnDbContext dbContext,
        ILogger<KhachLinkHomeSettingsController> logger) : ControllerBase
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ILogger<KhachLinkHomeSettingsController> _logger = logger;

        /// <summary>
        /// GET /api/platform/khachlink-home-settings — returns the single global config row.
        /// Anonymous: KhachLink PWA reads this on home page load (no auth needed).
        /// If no row exists (fresh deployment), returns default values (all sections ON).
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetSettings()
        {
            var settings = await _dbContext.KhachLinkHomeSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                // Return defaults without persisting
                return Ok(new KhachLinkHomeSettingsDto
                {
                    Home_CampaignSection_Enabled = true,
                    Home_StoreSection_Enabled = true,
                    Home_FeaturedSection_Enabled = true,
                    Home_SocialHub_Enabled = true,
                    ShowNavMenu = true
                });
            }

            return Ok(ToDto(settings));
        }

        /// <summary>
        /// PUT /api/platform/khachlink-home-settings — updates the global config (creates if not exists).
        /// SystemAdmin only (cookie auth from ShopERP admin UI).
        /// </summary>
        [HttpPut]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<IActionResult> UpdateSettings([FromBody] KhachLinkHomeSettingsDto body)
        {
            if (body == null)
                return BadRequest(new { error = "Body không được để trống." });

            string changedBy = User.Identity?.Name ?? "SystemAdmin";

            var settings = await _dbContext.KhachLinkHomeSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new KhachLinkHomeSettings();
                _ = _dbContext.KhachLinkHomeSettings.Add(settings);
            }

            settings.UpdateToggles(
                body.Home_CampaignSection_Enabled,
                body.Home_StoreSection_Enabled,
                body.Home_FeaturedSection_Enabled,
                body.Home_SocialHub_Enabled,
                changedBy,
                body.ShowNavMenu);

            _ = await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Updated KhachLink home settings by {ChangedBy}", changedBy);

            return Ok(ToDto(settings));
        }

        private static KhachLinkHomeSettingsDto ToDto(KhachLinkHomeSettings s) => new()
        {
            Home_CampaignSection_Enabled = s.Home_CampaignSection_Enabled,
            Home_StoreSection_Enabled = s.Home_StoreSection_Enabled,
            Home_FeaturedSection_Enabled = s.Home_FeaturedSection_Enabled,
            Home_SocialHub_Enabled = s.Home_SocialHub_Enabled,
            ShowNavMenu = s.ShowNavMenu
        };
    }

    /// <summary>
    /// #100: DTO for KhachLink home page section toggles (global, not tenant-scoped).
    /// Used by Gateway API + KhachLink HTTP service + ShopERP admin page.
    /// </summary>
    public class KhachLinkHomeSettingsDto
    {
        public bool Home_CampaignSection_Enabled { get; set; } = true;
        public bool Home_StoreSection_Enabled { get; set; } = true;
        public bool Home_FeaturedSection_Enabled { get; set; } = true;
        public bool Home_SocialHub_Enabled { get; set; } = true;
        /// <summary>#121.1.1: Show/hide vertical sidebar nav on KhachLink desktop. Default true.</summary>
        public bool ShowNavMenu { get; set; } = true;
    }
}
