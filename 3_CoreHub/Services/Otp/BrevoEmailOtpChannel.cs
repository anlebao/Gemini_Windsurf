using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services.Otp;

/// <summary>
/// W2 / P4 (2026-11-10, user duyệt): kênh email OTP — Brevo free tier 0đ.
/// Wrap <see cref="IEmailService"/> (BrevoEmailService đã có + tests).
/// </summary>
public class BrevoEmailOtpChannel(
    IEmailService emailService,
    ILogger<BrevoEmailOtpChannel> logger) : IIdentityOtpChannel
{
    public string ChannelName => "email";

    public async Task<bool> SendOtpAsync(string contact, string code, CancellationToken ct = default)
    {
        var html = $"""
            <p>Mã xác minh đăng ký Vạn An của bạn:</p>
            <p style="font-size:28px;font-weight:700;letter-spacing:4px">{code}</p>
            <p>Mã có hiệu lực 5 phút. Nếu bạn không yêu cầu mã này, vui lòng bỏ qua email.</p>
            """;
        var sent = await emailService.SendEmailAsync(contact, "Mã xác minh Vạn An", html, ct);
        if (!sent)
            logger.LogWarning("BrevoEmailOtpChannel: gửi OTP email thất bại — {Contact}", contact);
        return sent;
    }
}