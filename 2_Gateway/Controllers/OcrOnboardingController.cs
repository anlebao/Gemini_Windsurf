using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services.Onboarding;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// OCR Tenant Onboarding (S2 — 2026-10-25): tạo tenant từ kết quả scan
/// GPKD + biển hiệu + menu (OCR client-side trên wizard ShopERP).
/// SystemAdmin-only — dữ liệu đã qua bước human-in-the-loop review trên wizard.
/// Tất cả ghi vào PostgreSQL (Gateway DB — source of truth, Option C).
/// </summary>
[ApiController]
[Route("api/v1/ocr-onboarding")]
[Authorize(Policy = "SystemAdmin")]
public class OcrOnboardingController(
    ITenantOcrOnboardingService onboardingService,
    ILogger<OcrOnboardingController> logger) : ControllerBase
{
    private readonly ITenantOcrOnboardingService _onboardingService = onboardingService;
    private readonly ILogger<OcrOnboardingController> _logger = logger;

    /// <summary>
    /// Tạo tenant theo flow Pending → Verify từ dữ liệu OCR onboarding.
    /// Trả owner credentials HIỆN 1 LẦN (pattern ActivatedTenantCredential).
    /// Case A trùng MST → ExistingDuplicateTenantId (không tạo). Case B trùng
    /// tên+địa chỉ → vẫn tạo + gợi ý duplicate.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OcrOnboardingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OcrOnboardingResult>> Execute(
        [FromBody] OcrOnboardingRequest request,
        CancellationToken ct)
    {
        try
        {
            var adminId = GetAdminUserId();
            var result = await _onboardingService.ExecuteAsync(request, adminId, ct);
            _logger.LogInformation(
                "OCR Onboarding {Case}: tenant {TenantId} ({Name}) — duplicate={Duplicate}",
                request.Case, result.TenantId, request.Name, result.ExistingDuplicateTenantId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "OCR Onboarding validation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR Onboarding failed for tenant {Name} ({Case})", request.Name, request.Case);
            return StatusCode(500, new { error = "Không thể tạo tenant — xem log Gateway." });
        }
    }

    private Guid GetAdminUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("userId")?.Value;
        return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
    }
}