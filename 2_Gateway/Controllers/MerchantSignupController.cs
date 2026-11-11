using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.CoreHub.Services.Registrations;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// Self-Serve Merchant Signup (2026-11-09 — SRS FR-1..6, FR-19..22):
/// merchant tự onboard qua /onboard (KhachLink PWA) → Pending tenant.
/// Public + rate-limit + Turnstile + honeypot + master flag (503 khi đóng).
/// OTP endpoints = stub 501 (P4 — chờ ESMS keys).
/// </summary>
[ApiController]
public class MerchantSignupController(
    IMerchantSignupService signupService,
    IMerchantSignupOtpService otpService,
    ITurnstileVerificationService turnstileService,
    ILogger<MerchantSignupController> logger) : ControllerBase
{
    /// <summary>Submit self-serve signup — generic response, không tiết lộ duplicate.</summary>
    [HttpPost("api/v1/merchant-signups")]
    [AllowAnonymous]
    [EnableRateLimiting("merchant-signup")]
    public async Task<ActionResult<MerchantSignupResult>> Submit(
        [FromBody] MerchantSignupRequest request,
        CancellationToken ct = default)
    {
        try
        {
            // Honeypot: bot điền field ẩn → fake success, không lưu (pattern TenantRegistration)
            if (!string.IsNullOrEmpty(request.HoneypotWebsite))
            {
                logger.LogInformation("Merchant signup honeypot triggered — silent reject");
                return Ok(new MerchantSignupResult("Đăng ký thành công! Đội ngũ Vạn An sẽ liên hệ xác minh.", Guid.Empty));
            }

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (!await turnstileService.VerifyAsync(request.TurnstileToken, clientIp, ct))
                return BadRequest(new { error = "Xác thực chống bot thất bại. Vui lòng thử lại." });

            var userAgent = Request.Headers.UserAgent.ToString();
            var result = await signupService.SubmitAsync(request, clientIp, userAgent, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "self-serve-closed")
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Đăng ký tự phục vụ tạm đóng. Vui lòng liên hệ đội ngũ Vạn An." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Merchant signup submit failed ({Case})", request.Case);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Lỗi không xác định khi gửi đăng ký. Vui lòng thử lại." });
        }
    }

    /// <summary>P4 / ID-P3 (2026-11-10, user duyệt): config public cho UI wizard — requireOtp + channel.</summary>
    [HttpGet("api/v1/merchant-signups/config")]
    [AllowAnonymous]
    public IActionResult GetConfig() => Ok(new
    {
        requireOtp = otpService.RequiresOtp,
        otpChannel = otpService.OtpChannel,
    });

    /// <summary>P4 / ID-P3: gửi OTP (email-first Brevo 0đ). KHÔNG trả code — production an toàn.</summary>
    [HttpPost("api/v1/merchant-signups/otp/send")]
    [AllowAnonymous]
    [EnableRateLimiting("signup-otp")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Contact))
            return BadRequest(new { error = "Liên hệ không được để trống." });

        try
        {
            var result = await otpService.SendOtpAsync(request.Contact.Trim(), ct);
            if (!result.Sent)
                return BadRequest(new { error = "Không gửi được mã xác minh. Vui lòng thử lại sau." });
            return Ok(new { sent = true, retryAfterSeconds = result.RetryAfterSeconds });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>P4 / ID-P3: verify OTP → session token single-use (submit kèm OtpSessionToken).</summary>
    [HttpPost("api/v1/merchant-signups/otp/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("signup-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Contact) || string.IsNullOrWhiteSpace(request.Otp))
            return BadRequest(new { error = "Thiếu liên hệ hoặc mã xác minh." });

        var result = await otpService.VerifyOtpAsync(request.Contact.Trim(), request.Otp.Trim(), ct);
        if (!result.Verified)
            return BadRequest(new { error = result.Error ?? "Mã không đúng hoặc đã hết hạn." });
        return Ok(new { verified = true, sessionToken = result.SessionToken });
    }

    public record SendOtpRequest(string? Contact);
    public record VerifyOtpRequest(string? Contact, string? Otp);

    /// <summary>Public status lookup — anti-enumeration (sai cặp → NotFound generic).</summary>
    [HttpGet("api/v1/merchant-signups/status")]
    [AllowAnonymous]
    [EnableRateLimiting("signup-status")]
    public async Task<ActionResult<SignupStatusResult>> Status(
        [FromQuery] Guid signupId, [FromQuery] string phone, CancellationToken ct = default)
    {
        if (signupId == Guid.Empty || string.IsNullOrWhiteSpace(phone))
            return BadRequest(new { error = "Thiếu tham số." });

        return Ok(await signupService.GetStatusAsync(signupId, phone, ct));
    }
}
