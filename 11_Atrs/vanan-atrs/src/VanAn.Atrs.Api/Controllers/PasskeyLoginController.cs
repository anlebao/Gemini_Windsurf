using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// ATRS-21 — PUBLIC passkey login (customer không cần Google/Facebook/SMS).
/// Discoverable credential ceremony: không auth header — chính assertion là proof.
/// Rate limit per-IP (begin) chống abuse; mọi lỗi trả generic (anti-enumeration).
/// </summary>
[ApiController]
[Route("api/v1/account-trust/passkey/login")]
public class PasskeyLoginController(
    IWebAuthnService webAuthnService,
    IMemoryCache cache,
    IConfiguration configuration,
    ILogger<PasskeyLoginController> logger) : ControllerBase
{
    private int MaxBeginPerHour => configuration.GetValue("Atrs:PasskeyLogin:MaxBeginPerHour", 20);
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    [HttpPost("begin")]
    public async Task<IActionResult> Begin([FromBody] BeginPasskeyLoginRequest request, CancellationToken ct)
    {
        if (IsRateLimited())
            return StatusCode(429, new { error = "Quá nhiều yêu cầu. Vui lòng thử lại sau." });

        try
        {
            var result = await webAuthnService.BeginPasskeyLoginAsync(request.Origin, request.RpId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("finish")]
    public async Task<IActionResult> Finish([FromBody] FinishPasskeyLoginRequest request, CancellationToken ct)
    {
        var result = await webAuthnService.FinishPasskeyLoginAsync(
            request.Challenge, request.Origin, request.RpId,
            request.AssertionResponse, request.DeviceId, ct);

        if (!result.Success)
        {
            // Anti-enumeration: client chỉ nhận success/fail generic — ReasonCode nội bộ cho log.
            logger.LogInformation("Passkey login failed: {Reason}", result.ReasonCode);
            return BadRequest(new { success = false, error = "Đăng nhập không thành công. Vui lòng thử lại." });
        }

        logger.LogInformation("Passkey login success for customer {CustomerId} (created={Created})",
            result.CustomerId, result.CustomerCreated);
        return Ok(new
        {
            success = true,
            customerToken = result.CustomerToken,
            customerId = result.CustomerId,
            customerCreated = result.CustomerCreated
        });
    }

    private bool IsRateLimited()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = $"passkey-login-begin:{ip}";
        var count = cache.GetOrCreate(key, e =>
        {
            e.AbsoluteExpirationRelativeToNow = Window;
            return 0;
        });
        if (count >= MaxBeginPerHour)
            return true;
        cache.Set(key, count + 1, Window);
        return false;
    }

    public sealed record BeginPasskeyLoginRequest(string Origin, string RpId);
    public sealed record FinishPasskeyLoginRequest(
        string Challenge, string Origin, string RpId,
        JsonElement AssertionResponse, string? DeviceId);
}
