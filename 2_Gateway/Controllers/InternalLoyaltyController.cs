using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.Gateway.Filters;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// Loyalty Consistency Fix Phase 0 (Option B): internal service-to-service API for ShopERP
/// to access Alliance wallet + mode resolution without direct PG connection (multi-VPS ready).
///
/// Auth: [InternalApiKey] (custom IAsyncAuthorizationFilter) validates X-Internal-Api-Key header
/// against InternalLoyalty:ApiKey config. [AllowAnonymous] suppresses the default JWT/Cookie auth
/// pipeline — internal endpoints use API key, not user credentials. Architecture test W12-G7
/// requires class-level [Authorize] OR [AllowAnonymous] on all Gateway controllers.
/// </summary>
[ApiController]
[Route("api/internal/loyalty")]
[AllowAnonymous]
[InternalApiKey]
public class InternalLoyaltyController(
    ILoyaltyModeResolver modeResolver,
    IAllianceWalletService walletService,
    ILogger<InternalLoyaltyController> logger) : ControllerBase
{
    private readonly ILoyaltyModeResolver _modeResolver = modeResolver;
    private readonly IAllianceWalletService _walletService = walletService;
    private readonly ILogger<InternalLoyaltyController> _logger = logger;

    /// <summary>
    /// GET /api/internal/loyalty/effective-config/{tenantId}
    /// Returns the effective mode config for a tenant (tenant override → global fallback).
    /// Cached 60s on the ShopERP caller side (LoyaltyModeResolverHttpProxy).
    /// </summary>
    [HttpGet("effective-config/{tenantId}")]
    public async Task<IActionResult> GetEffectiveConfig(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "TenantId không hợp lệ." });

        var mode = await _modeResolver.GetEffectiveModeAsync(tenantId);
        var maxWallet = await _modeResolver.GetEffectiveMaxWalletPointsAsync(tenantId);
        var isMember = await _modeResolver.IsAllianceMemberAsync(tenantId);

        return Ok(new EffectiveConfigResponse
        {
            Mode = mode.ToString(),
            MaxWalletPoints = maxWallet,
            IsAllianceMember = isMember
        });
    }

    /// <summary>POST /api/internal/loyalty/points/add — adds points to PG wallet (idempotent).</summary>
    [HttpPost("points/add")]
    public async Task<IActionResult> AddPoints([FromBody] AddPointsRequest req)
    {
        if (req is null || req.Points <= 0)
            return BadRequest(new { success = false, error = "Invalid request body or points." });

        var (success, balance, error) = await _walletService.AddPointsAsync(
            req.CustomerDeviceId, req.TenantId, req.Points, req.Reason,
            req.SourceOrderId, req.IdempotencyKey);

        return success
            ? Ok(new PointsResponse { Success = true, NewBalance = balance })
            : BadRequest(new PointsResponse { Success = false, Error = error });
    }

    /// <summary>POST /api/internal/loyalty/points/deduct — deducts points from PG wallet (idempotent).</summary>
    [HttpPost("points/deduct")]
    public async Task<IActionResult> DeductPoints([FromBody] DeductPointsRequest req)
    {
        if (req is null || req.Points <= 0)
            return BadRequest(new { success = false, error = "Invalid request body or points." });

        var (success, balance, error) = await _walletService.DeductPointsAsync(
            req.CustomerDeviceId, req.TenantId, req.Points, req.Reason,
            req.VoucherCode, req.IdempotencyKey);

        return success
            ? Ok(new PointsResponse { Success = true, NewBalance = balance })
            : BadRequest(new PointsResponse { Success = false, Error = error });
    }

    /// <summary>POST /api/internal/loyalty/points/refund — refunds points to PG wallet (idempotent).</summary>
    [HttpPost("points/refund")]
    public async Task<IActionResult> RefundPoints([FromBody] RefundPointsRequest req)
    {
        if (req is null || req.Points <= 0)
            return BadRequest(new { success = false, error = "Invalid request body or points." });

        var (success, balance, error) = await _walletService.RefundAsync(
            req.CustomerDeviceId, req.TenantId, req.Points, req.Reason,
            req.VoucherCode, req.IdempotencyKey);

        return success
            ? Ok(new PointsResponse { Success = true, NewBalance = balance })
            : BadRequest(new PointsResponse { Success = false, Error = error });
    }

    /// <summary>
    /// GET /api/internal/loyalty/wallet/{deviceId} — returns wallet balance for read paths.
    /// Cached 10s on the ShopERP caller side (AllianceWalletServiceHttpProxy).
    /// </summary>
    [HttpGet("wallet/{deviceId}")]
    public async Task<IActionResult> GetWallet(Guid deviceId)
    {
        if (deviceId == Guid.Empty)
            return BadRequest(new { error = "DeviceId không hợp lệ." });

        var wallet = await _walletService.GetWalletByDeviceIdAsync(deviceId);
        if (wallet is null)
        {
            return Ok(new InternalWalletResponse { TotalPointBalance = 0, IsActive = false });
        }

        return Ok(new InternalWalletResponse
        {
            TotalPointBalance = wallet.TotalPointBalance,
            IsActive = wallet.IsActive
        });
    }
}

// === Response / Request DTOs ===

public class EffectiveConfigResponse
{
    public string Mode { get; set; } = string.Empty;
    public int MaxWalletPoints { get; set; }
    public bool IsAllianceMember { get; set; }
}

public class PointsResponse
{
    public bool Success { get; set; }
    public int NewBalance { get; set; }
    public string? Error { get; set; }
}

public class InternalWalletResponse
{
    public int TotalPointBalance { get; set; }
    public bool IsActive { get; set; }
}

public class AddPointsRequest
{
    public Guid CustomerDeviceId { get; set; }
    public Guid TenantId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? SourceOrderId { get; set; }
    public string? IdempotencyKey { get; set; }
}

public class DeductPointsRequest
{
    public Guid CustomerDeviceId { get; set; }
    public Guid TenantId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? VoucherCode { get; set; }
    public string? IdempotencyKey { get; set; }
}

public class RefundPointsRequest
{
    public Guid CustomerDeviceId { get; set; }
    public Guid TenantId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string VoucherCode { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
}
