using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.Gateway.Services;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// Business-info lookup by MST (tax code) — 2026-10-01.
/// Returns public business registration data (Luật Doanh nghiệp 2020) via MstLookupService:
/// local-first (VanAn PG tenants) → doanhnghiep.vn fallback (API key server-side, cache + rate limit).
/// Used by ShopERP phiếu thu/phiếu chi forms to auto-fill counterparty info.
/// </summary>
[ApiController]
[Route("api/v1/business-info")]
[Authorize]
public class BusinessLookupController(
    IMstLookupService lookupService,
    ILogger<BusinessLookupController> logger) : ControllerBase
{
    /// <summary>Lookup business info by MST. Returns 404 when not found, 429 on daily quota, 502 on upstream failure.</summary>
    [HttpGet("{mst}")]
    [ProducesResponseType(typeof(BusinessLookupResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByMst(string mst, CancellationToken ct = default)
    {
        var normalized = mst?.Trim() ?? "";
        if (!IsValidTaxCode(normalized))
        {
            logger.LogInformation("BusinessLookup: invalid MST format '{Mst}'", normalized);
            return BadRequest(new { error = "Mã số thuế không hợp lệ (phải 10 hoặc 13 chữ số)." });
        }

        try
        {
            var result = await lookupService.LookupByTaxCodeAsync(normalized, ct);
            if (result is null)
                return NotFound(new { error = "Không tìm thấy doanh nghiệp với mã số thuế này." });
            return Ok(result);
        }
        catch (BusinessLookupRateLimitedException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = ex.Message });
        }
        catch (BusinessLookupUnavailableException ex)
        {
            logger.LogWarning(ex, "BusinessLookup: upstream unavailable for MST {Mst}", normalized);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>Vietnamese tax code: 10 digits, or 13 digits with optional -xxx branch suffix.</summary>
    private static bool IsValidTaxCode(string taxCode)
    {
        if (string.IsNullOrWhiteSpace(taxCode)) return false;
        var digits = taxCode.Replace("-", "").Trim();
        return (digits.Length == 10 || digits.Length == 13) && digits.All(char.IsDigit);
    }
}
