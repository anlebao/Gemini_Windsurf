using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services.Otp;

/// <summary>
/// W2 / P4 (2026-11-10, user duyệt): composite fallback — thử primary channel,
/// fail → thử kênh kế tiếp (config MerchantSignup:OtpFallbackChannels, default "email").
/// NHẬN concrete channels (KHÔNG IEnumerable&lt;IIdentityOtpChannel&gt;) để tránh DI
/// recursion khi bản thân fallback được đăng ký là IIdentityOtpChannel.
/// ZNS/SMS thêm sau → thêm ctor param + match trong map.
/// </summary>
public class FallbackOtpChannel(
    BrevoEmailOtpChannel emailChannel,
    IConfiguration configuration,
    ILogger<FallbackOtpChannel> logger) : IIdentityOtpChannel
{
    public string ChannelName => "email";

    public async Task<bool> SendOtpAsync(string contact, string code, CancellationToken ct = default)
    {
        var names = (configuration.GetValue("MerchantSignup:OtpFallbackChannels", "email") ?? "email")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var name in names)
        {
            IIdentityOtpChannel? channel = name.ToLowerInvariant() switch
            {
                "email" => emailChannel,
                // "sms" => smsChannel,   // thêm khi có eSMS
                // "zns" => znsChannel,   // thêm khi có Zalo OA
                _ => null
            };
            if (channel is null) continue;

            if (await channel.SendOtpAsync(contact, code, ct))
                return true;

            logger.LogWarning("FallbackOtpChannel: kênh {Channel} gửi OTP thất bại — thử kênh kế tiếp", name);
        }
        return false;
    }
}