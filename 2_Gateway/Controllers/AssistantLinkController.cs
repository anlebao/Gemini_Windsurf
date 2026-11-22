using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// UNIVERSAL AI ASSISTANT W1 (2026-11-22): owner quản lý hosted AI Link per tenant.
/// Link = slug high-entropy → tenant cho public assistant page (/a/{slug} trên KhachLink).
/// Link KHÔNG chứa config — persona/knowledge/publish sống ở TenantAI + AIChat_Enabled flag.
///
/// Endpoints (tenant từ JWT tenant_id claim — RequireTenantAccess):
///   GET    /api/v1/assistant-link            → list links của tenant
///   POST   /api/v1/assistant-link            → tạo link mới {label?}
///   POST   /api/v1/assistant-link/{id}/revoke    → thu hồi (IsActive=false)
///   POST   /api/v1/assistant-link/{id}/regenerate → revoke + tạo slug mới
///   GET    /api/v1/assistant-link/{id}/qr.png    → PNG QR encode public URL
/// </summary>
[ApiController]
[Route("api/v1/assistant-link")]
[Authorize(Policy = "RequireTenantAccess")]
public class AssistantLinkController(
    VanAnDbContext dbContext,
    ITenantProvider tenantProvider,
    IQrCodeService qrCodeService,
    IConfiguration configuration,
    ILogger<AssistantLinkController> logger) : ControllerBase
{
    private readonly VanAnDbContext _dbContext = dbContext;
    private readonly ITenantProvider _tenantProvider = tenantProvider;
    private readonly IQrCodeService _qrCodeService = qrCodeService;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<AssistantLinkController> _logger = logger;

    /// <summary>Base URL public cho AI Link — config AssistantLink:PublicBaseUrl, fallback ExternalUrls:KhachLink.</summary>
    private string PublicBaseUrl
        => (_configuration["AssistantLink:PublicBaseUrl"]
            ?? _configuration["ExternalUrls:KhachLink"]
            ?? string.Empty).TrimEnd('/');

    private Guid TenantId => _tenantProvider.TenantId;

    /// <summary>
    /// Public URL = storefront deep-link có sẵn: {base}/store/{storeSlug}?chat=ai
    /// (?chat=ai → AiChatWidget.OpenPanel consume-once trên Store.razor).
    /// Fallback /a/{slug} chỉ khi tenant chưa có storefront slug.
    /// </summary>
    private string PublicUrl(string assistantSlug, string? storeSlug)
        => !string.IsNullOrWhiteSpace(storeSlug)
            ? $"{PublicBaseUrl}/store/{storeSlug.Trim()}?chat=ai"
            : $"{PublicBaseUrl}/a/{assistantSlug}";

    /// <summary>Storefront slug của tenant (Tenant.Settings.Slug → route /store/{slug}).</summary>
    private async Task<string?> StorefrontSlugAsync(CancellationToken ct)
        => await _dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == new TenantId(TenantId))
            .Select(t => t.Settings != null ? t.Settings.Slug : null)
            .FirstOrDefaultAsync(ct);

    private static readonly char[] SlugAlphabet =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

    /// <summary>GET — danh sách link (active trước, mới nhất trước).</summary>
    [HttpGet]
    public async Task<ActionResult<List<AssistantLinkDto>>> List(CancellationToken ct)
    {
        var links = await _dbContext.AssistantLinks
            .Where(l => l.TenantId == new TenantId(TenantId))
            .OrderByDescending(l => l.IsActive)
            .ThenByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        var storeSlug = await StorefrontSlugAsync(ct);
        return Ok(links.Select(l => ToDto(l, PublicUrl(l.Slug, storeSlug), EmbedUrl(l.Slug))).ToList());
    }

    /// <summary>POST — tạo link mới. Slug do server sinh (10 ký tự base62 ≈ 59 bits entropy).</summary>
    [HttpPost]
    public async Task<ActionResult<AssistantLinkDto>> Create(
        [FromBody] CreateAssistantLinkRequest? request, CancellationToken ct)
    {
        if (TenantId == Guid.Empty)
            return Unauthorized(new { error = "tenant_id claim missing" });

        var link = await CreateWithUniqueSlugAsync(request?.Label, ct);
        if (link is null)
            return StatusCode(500, new { error = "Không tạo được link, vui lòng thử lại." });

        _logger.LogInformation("AssistantLink created tenant={Tenant} slug={Slug}", TenantId, link.Slug);
        return Ok(ToDto(link, await PublicUrlAsync(link, ct), EmbedUrl(link.Slug)));
    }

    /// <summary>POST {id}/revoke — thu hồi link (public resolution trả 404 ngay).</summary>
    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var link = await FindOwnedAsync(id, ct);
        if (link is null) return NotFound(new { error = "Link không tồn tại." });
        if (!link.IsActive) return Ok(new { revoked = true });

        link.Revoke(User.Identity?.Name ?? _tenantProvider.CurrentUser);
        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("AssistantLink revoked tenant={Tenant} slug={Slug}", TenantId, link.Slug);
        return Ok(new { revoked = true });
    }

    /// <summary>POST {id}/regenerate — revoke slug cũ + tạo slug mới (QR/link cũ chết ngay).</summary>
    [HttpPost("{id:guid}/regenerate")]
    public async Task<ActionResult<AssistantLinkDto>> Regenerate(Guid id, CancellationToken ct)
    {
        var link = await FindOwnedAsync(id, ct);
        if (link is null) return NotFound(new { error = "Link không tồn tại." });

        link.Revoke(User.Identity?.Name ?? _tenantProvider.CurrentUser);
        var fresh = await CreateWithUniqueSlugAsync(link.Label, ct);
        if (fresh is null)
            return StatusCode(500, new { error = "Không tạo được link mới, vui lòng thử lại." });

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("AssistantLink regenerated tenant={Tenant} old={Old} new={New}",
            TenantId, link.Slug, fresh.Slug);
        return Ok(ToDto(fresh, await PublicUrlAsync(fresh, ct), EmbedUrl(fresh.Slug)));
    }

    /// <summary>GET {id}/qr.png — QR PNG encode public URL (in dán/quầy, Zalo share).</summary>
    [HttpGet("{id:guid}/qr.png")]
    public async Task<IActionResult> Qr(Guid id, CancellationToken ct)
    {
        var link = await FindOwnedAsync(id, ct);
        if (link is null || !link.IsActive) return NotFound(new { error = "Link không tồn tại." });

        var png = _qrCodeService.GenerateQRCode(await PublicUrlAsync(link, ct));
        return File(png, "image/png");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<AssistantLink?> FindOwnedAsync(Guid id, CancellationToken ct)
        => await _dbContext.AssistantLinks
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == new TenantId(TenantId), ct);

    private async Task<AssistantLink?> CreateWithUniqueSlugAsync(string? label, CancellationToken ct)
    {
        var tid = new TenantId(TenantId);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var slug = GenerateSlug();
            var exists = await _dbContext.AssistantLinks
                .IgnoreQueryFilters()
                .AnyAsync(l => l.Slug == slug, ct);
            if (exists) continue;

            var link = new AssistantLink(tid, slug, label);
            _dbContext.AssistantLinks.Add(link);
            try
            {
                await _dbContext.SaveChangesAsync(ct);
                return link;
            }
            catch (DbUpdateException)
            {
                _dbContext.Entry(link).State = EntityState.Detached;
            }
        }
        return null;
    }

    private static string GenerateSlug()
    {
        Span<byte> bytes = stackalloc byte[10];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[10];
        for (var i = 0; i < 10; i++)
            chars[i] = SlugAlphabet[bytes[i] % SlugAlphabet.Length];
        return new string(chars);
    }

    private async Task<string> PublicUrlAsync(AssistantLink link, CancellationToken ct)
        => PublicUrl(link.Slug, await StorefrontSlugAsync(ct));

    /// <summary>W2a iframe embed URL — hosted assistant page in minimal chrome.</summary>
    private string EmbedUrl(string slug) => $"{PublicBaseUrl}/a/{slug}?embed=1";

    private AssistantLinkDto ToDto(AssistantLink l, string publicUrl, string embedUrl) => new(
        l.Id, l.Slug, l.Label, l.IsActive, l.CreatedAt, publicUrl, embedUrl);
}

public sealed record CreateAssistantLinkRequest(string? Label);

public sealed record AssistantLinkDto(
    Guid Id, string Slug, string? Label, bool IsActive,
    DateTime CreatedAt, string PublicUrl, string EmbedUrl);
