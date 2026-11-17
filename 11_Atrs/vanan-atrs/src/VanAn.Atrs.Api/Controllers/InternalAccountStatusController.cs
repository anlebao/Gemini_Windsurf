using Microsoft.AspNetCore.Mvc;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// ATRS-17 (FR-001) — internal: Gateway sync account lock/delete (ShopERP → NATS → Gateway → atrs).
/// Guard: X-Internal-Api-Key (Atrs:InternalApiKey). Best-effort: account chưa có profile → 200 no-op.
/// </summary>
[ApiController]
[Route("api/v1/internal/account-status")]
public class InternalAccountStatusController(
    ITrustProfileService trustProfileService,
    IConfiguration configuration,
    ILogger<InternalAccountStatusController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SetStatus([FromBody] SetAccountStatusRequest request, CancellationToken ct)
    {
        var expectedKey = configuration["Atrs:InternalApiKey"];
        var providedKey = Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(expectedKey) || !string.Equals(expectedKey, providedKey, StringComparison.Ordinal))
            return Unauthorized(new { error = "Internal key không hợp lệ." });

        if (!Enum.TryParse<TrustIdentityType>(request.IdentityType, true, out var identityType))
            return BadRequest(new { error = $"IdentityType không hợp lệ: {request.IdentityType}" });
        if (!Enum.TryParse<TrustProfileStatus>(request.Status, true, out var status))
            return BadRequest(new { error = $"Status không hợp lệ: {request.Status}" });

        var ok = await trustProfileService.SetStatusAsync(identityType, request.IdentityId, status, ct);
        logger.LogInformation("Account status sync: {Type} {Id} → {Status} (applied={Applied})",
            request.IdentityType, request.IdentityId, request.Status, ok);
        return Ok(new { applied = ok });
    }

    public sealed record SetAccountStatusRequest(string IdentityType, Guid IdentityId, string Status);
}