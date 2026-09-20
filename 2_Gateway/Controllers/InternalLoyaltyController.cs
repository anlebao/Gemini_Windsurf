using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
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
    ILoyaltyPointLedgerService ledgerService,
    ILogger<InternalLoyaltyController> logger) : ControllerBase
{
    private readonly ILoyaltyModeResolver _modeResolver = modeResolver;
    private readonly IAllianceWalletService _walletService = walletService;
    private readonly ILoyaltyPointLedgerService _ledgerService = ledgerService;
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

    // ══════════════════════════════════════════════════════════
    // Loyalty Points Integrity (Batch 2) — PG ledger endpoints.
    // ShopERP POS proxies route ALL loyalty writes here (single source of truth).
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// POST /api/internal/loyalty/award — award points via the PG ledger (budget check → mode
    /// routing → issuance record → mirror sync). Centralized orderId double-award guard (RC3).
    /// </summary>
    [HttpPost("award")]
    public async Task<IActionResult> Award([FromBody] AwardRequest request, CancellationToken ct)
    {
        if (request is null || request.Points <= 0)
            return BadRequest(new { success = false, error = "Invalid request body or points." });

        LedgerResult result = await _ledgerService.AwardAsync(request, ct);
        return result.Status switch
        {
            LedgerOperationStatus.Skipped => Ok(new LedgerResponse
            {
                Success = true,
                Skipped = true,
                Message = result.Error,
                NewBalance = result.NewBalance
            }),
            LedgerOperationStatus.Success => Ok(new LedgerResponse { Success = true, NewBalance = result.NewBalance }),
            LedgerOperationStatus.Rejected => Conflict(new LedgerResponse { Success = false, Error = result.Error }),
            _ => StatusCode(500, new LedgerResponse { Success = false, Error = result.Error })
        };
    }

    /// <summary>
    /// POST /api/internal/loyalty/spend — spend points via the PG ledger. Silo: only the redeeming
    /// tenant's row ("luật Silo"). Alliance: wallet deduct.
    /// NOTE: fully-qualified VanAn.CoreHub.Services.SpendRequest — a different SpendRequest
    /// (Community Fund) exists in this namespace.
    /// </summary>
    [HttpPost("spend")]
    public async Task<IActionResult> Spend([FromBody] VanAn.CoreHub.Services.SpendRequest request, CancellationToken ct)
    {
        if (request is null || request.Points <= 0)
            return BadRequest(new { success = false, error = "Invalid request body or points." });

        LedgerResult result = await _ledgerService.SpendAsync(request, ct);
        return result.Status switch
        {
            LedgerOperationStatus.Skipped => Ok(new LedgerResponse
            {
                Success = true,
                Skipped = true,
                Message = result.Error,
                NewBalance = result.NewBalance
            }),
            LedgerOperationStatus.Success => Ok(new LedgerResponse { Success = true, NewBalance = result.NewBalance }),
            LedgerOperationStatus.Rejected => Conflict(new LedgerResponse { Success = false, Error = result.Error }),
            _ => StatusCode(500, new LedgerResponse { Success = false, Error = result.Error })
        };
    }

    /// <summary>
    /// POST /api/internal/loyalty/refund — refund points (redemption cancel) via the PG ledger.
    /// </summary>
    [HttpPost("refund")]
    public async Task<IActionResult> Refund([FromBody] RefundRequest request, CancellationToken ct)
    {
        if (request is null || request.Points <= 0)
            return BadRequest(new { success = false, error = "Invalid request body or points." });

        LedgerResult result = await _ledgerService.RefundAsync(request, ct);
        return result.Status switch
        {
            LedgerOperationStatus.Skipped => Ok(new LedgerResponse
            {
                Success = true,
                Skipped = true,
                Message = result.Error,
                NewBalance = result.NewBalance
            }),
            LedgerOperationStatus.Success => Ok(new LedgerResponse { Success = true, NewBalance = result.NewBalance }),
            LedgerOperationStatus.Rejected => Conflict(new LedgerResponse { Success = false, Error = result.Error }),
            _ => StatusCode(500, new LedgerResponse { Success = false, Error = result.Error })
        };
    }

    /// <summary>
    /// POST /api/internal/loyalty/revert-order — reverse all non-reversed issuance records of an order
    /// (mode-aware). Used by ShopERP/refund flows (Phase 4 reversal).
    /// </summary>
    [HttpPost("revert-order")]
    public async Task<IActionResult> RevertOrder([FromBody] RevertOrderRequest request, CancellationToken ct)
    {
        if (request is null || request.OrderId == Guid.Empty)
            return BadRequest(new { success = false, error = "Invalid request body." });

        int reversed = await _ledgerService.RevertOrderAsync(request.OrderId, new TenantId(request.TenantId), request.Reason ?? "Refund reversal", ct);
        return Ok(new { success = true, pointsReversed = reversed });
    }

    /// <summary>
    /// GET /api/internal/loyalty/balance?customerId=&amp;tenantId= — effective balance (Silo row /
    /// Alliance wallet). Read fallback for ShopERP mirror (LoyaltyReadRouter, decision T2.4).
    /// </summary>
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance([FromQuery] Guid customerId, [FromQuery] Guid tenantId, CancellationToken ct)
    {
        if (customerId == Guid.Empty || tenantId == Guid.Empty)
            return BadRequest(new { error = "customerId/tenantId không hợp lệ." });

        int balance = await _ledgerService.GetBalanceAsync(customerId, tenantId, ct);
        return Ok(new { customerId, tenantId, balance });
    }

    /// <summary>
    /// GET /api/internal/loyalty/awarded?orderId=&amp;tenantId= — points ACTUALLY awarded for an order
    /// (PG issuance record). Banner reads the real number, not a recompute (decision D4, RC2.2).
    /// Null when the order was never awarded.
    /// </summary>
    [HttpGet("awarded")]
    public async Task<IActionResult> GetAwarded([FromQuery] Guid orderId, [FromQuery] Guid tenantId, CancellationToken ct)
    {
        if (orderId == Guid.Empty || tenantId == Guid.Empty)
            return BadRequest(new { error = "orderId/tenantId không hợp lệ." });

        int? awarded = await _ledgerService.GetAwardedPointsAsync(orderId, tenantId, ct);
        return Ok(new { orderId, tenantId, awardedPoints = awarded });
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

// === Batch 2 ledger response DTO ===

public class LedgerResponse
{
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public int NewBalance { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class RevertOrderRequest
{
    public Guid OrderId { get; set; }
    public Guid TenantId { get; set; }
    public string? Reason { get; set; }
}
