using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VanAn.Gateway.Services;
using VanAn.Shared.Domain.Common;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// Task P13 — anonymous storefront top-up. The suspended KhachLink page knows the
/// tenantId but cannot hold metering caller secrets (WASM = public), so Gateway
/// proxies top-up creation + dynamic-QR minting server-side.
///
/// Guardrails:
/// - Only tenants whose wallet AlertLevel is set (warn50/warn90/depleted) may
///   create public top-ups — healthy tenants cannot be spammed with QR requests.
/// - Metering itself caps pending top-ups (5) and creations/hour (20) per tenant.
/// - Same "public-metering" rate limit (60 req/min/IP) as the ingest endpoint.
/// - Response never exposes wallet balance or ledger data.
/// </summary>
[ApiController]
[Route("api/public/metering")]
[EnableRateLimiting("public-metering")]
public sealed class PublicTopupController(IMeteringClient metering, ILogger<PublicTopupController> logger) : ControllerBase
{
    /// <summary>Packages + whether this tenant may create a public top-up right now.</summary>
    [AllowAnonymous]
    [HttpGet("topup-options")]
    public async Task<IActionResult> Options([FromQuery] string? tenantId, CancellationToken ct)
    {
        if (!Guid.TryParse(tenantId, out var tid) || tid == Guid.Empty)
            return BadRequest(new { error = "invalid_tenantId" });

        var wallet = await metering.GetWalletAsync(tid, ct);
        var allowed = wallet?.AlertLevel != null; // low/depleted only — anti-spam
        var packages = allowed ? await metering.ListPackagesAsync(ct) : Array.Empty<MeteringPackageInfo>();
        return Ok(new
        {
            allowed,
            packages = packages.Select(p => new
            {
                p.Code, p.Name, p.PriceVnd, p.Credits, p.ValidityDays, p.SortOrder
            })
        });
    }

    public sealed record PublicTopupRequest(
        string? TenantId, string? PackageCode, decimal? AmountVnd, string? IdempotencyKey);

    /// <summary>Create a pending top-up and mint its Tingee dynamic QR in one call.</summary>
    [AllowAnonymous]
    [HttpPost("topups")]
    public async Task<IActionResult> Create([FromBody] PublicTopupRequest? req, CancellationToken ct)
    {
        if (req is null || !Guid.TryParse(req.TenantId, out var tenantId) || tenantId == Guid.Empty)
            return BadRequest(new { error = "invalid_tenantId" });
        if (req.AmountVnd is < 0)
            return BadRequest(new { error = "invalid_amountVnd" });
        if (string.IsNullOrWhiteSpace(req.PackageCode) && req.AmountVnd is null or <= 0)
            return BadRequest(new { error = "amountVnd_or_packageCode_required" });

        // Re-check the guard server-side — never trust the earlier options call.
        var wallet = await metering.GetWalletAsync(tenantId, ct);
        if (wallet?.AlertLevel == null)
            return StatusCode(403, new { error = "topup_not_allowed" });

        var idem = string.IsNullOrWhiteSpace(req.IdempotencyKey)
            ? $"pub-topup:{tenantId}:{Guid.NewGuid():N}"
            : req.IdempotencyKey!.Length <= 128 ? req.IdempotencyKey : null;
        if (idem == null)
            return BadRequest(new { error = "invalid_idempotencyKey" });

        var topup = await metering.CreateTopupAsync(
            tenantId, req.AmountVnd, req.PackageCode, idem, ct);
        if (topup == null)
            return StatusCode(502, new { error = "topup_unavailable" });

        // Dynamic QR is best-effort — a null payload still returns the top-up so the
        // client can fall back to the ShopERP wallet page.
        var qr = await metering.CreateTopupQrAsync(topup.Id, ct);
        return Ok(new
        {
            topup.ReferenceCode,
            topup.AmountVnd,
            topup.Status,
            topup.ExpiresAtUtc,
            qrPayload = qr?.QrPayload,
            billId = qr?.BillId
        });
    }
}
