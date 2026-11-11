using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Services.Otp;

namespace VanAn.CoreHub.Services.Onboarding;

/// <summary>Kết quả gửi OTP — KHÔNG trả code (production an toàn).</summary>
public record OtpSendResult(bool Sent, int RetryAfterSeconds);

/// <summary>Kết quả verify OTP — thành công trả session token single-use (submit kèm token).</summary>
public record OtpVerifyResult(bool Verified, string? SessionToken, int? LockedMinutes, string? Error);

/// <summary>Session OTP đã verify — submit check: purpose + contact khớp + chưa hết hạn.</summary>
public record OtpSession(string Contact, string Purpose, DateTime ExpiresAtUtc);

/// <summary>
/// W2 / P4 (2026-11-10, user duyệt — ID-P3 L2 channel, email-first Brevo 0đ):
/// OTP merchant signup — Gateway-side, IMemoryCache (single instance VPS — ghi nợ Redis khi scale).
/// CSPRNG (RandomNumberGenerator — KHÔNG copy ShopERP OtpService cũ) · single-use ·
/// attempt lock · purpose binding "merchant-signup" · daily limit per contact.
/// </summary>
public interface IMerchantSignupOtpService
{
    bool RequiresOtp { get; }
    string OtpChannel { get; }

    /// <summary>Gửi OTP tới contact (email theo channel config). Rate-limit + daily cap nội bộ.</summary>
    Task<OtpSendResult> SendOtpAsync(string contact, CancellationToken ct = default);

    /// <summary>Verify code — đúng → session token single-use (TTL 30').</summary>
    Task<OtpVerifyResult> VerifyOtpAsync(string contact, string otp, CancellationToken ct = default);

    /// <summary>Submit check: resolve + consume session token (1 lần dùng).</summary>
    Task<OtpSession?> ConsumeSessionAsync(string token);
}

public class MerchantSignupOtpService(
    IMemoryCache cache,
    IConfiguration configuration,
    IIdentityOtpChannel channel,
    ILogger<MerchantSignupOtpService> logger) : IMerchantSignupOtpService
{
    public const string Purpose = "merchant-signup";
    public const string CodeCacheKeyPrefix = "signup-otp";
    private const int CodeTtlMinutes = 5;
    private const int SessionTtlMinutes = 30;

    private readonly bool _requireOtp = configuration.GetValue("MerchantSignup:RequireOtp", false);
    private readonly string _otpChannel = configuration.GetValue("MerchantSignup:OtpChannel", "email");
    private readonly int _maxAttempts = configuration.GetValue("MerchantSignup:OtpMaxAttempts", 5);
    private readonly int _lockoutHours = configuration.GetValue("MerchantSignup:OtpLockoutHours", 24);
    private readonly int _dailyLimit = configuration.GetValue("MerchantSignup:OtpDailyLimitPerContact", 5);
    private readonly int _resendCooldownSeconds = configuration.GetValue("MerchantSignup:OtpResendCooldownSeconds", 60);

    public bool RequiresOtp => _requireOtp;
    public string OtpChannel => _otpChannel;

    public async Task<OtpSendResult> SendOtpAsync(string contact, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(contact))
            throw new ArgumentException("Liên hệ không được để trống.", nameof(contact));

        // Daily cap per contact — chống spam OTP
        var dayKey = $"{CodeCacheKeyPrefix}-count:{contact}:{DateTime.UtcNow:yyyyMMdd}";
        var sentToday = cache.TryGetValue(dayKey, out int today) ? today : 0;
        if (sentToday >= _dailyLimit)
        {
            logger.LogWarning("OTP daily limit exceeded — {Contact}", contact);
            return new OtpSendResult(false, _resendCooldownSeconds);
        }

        // Resend cooldown
        var lastSentKey = $"{CodeCacheKeyPrefix}-sent:{contact}";
        if (cache.TryGetValue(lastSentKey, out DateTime lastSent))
        {
            var remaining = _resendCooldownSeconds - (int)(DateTime.UtcNow - lastSent).TotalSeconds;
            if (remaining > 0)
                return new OtpSendResult(true, Math.Max(remaining, 1));
        }

        var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
        cache.Set($"{CodeCacheKeyPrefix}:{contact}", code, TimeSpan.FromMinutes(CodeTtlMinutes));
        cache.Remove($"{CodeCacheKeyPrefix}-attempt:{contact}");
        cache.Remove($"{CodeCacheKeyPrefix}-lock:{contact}");

        var sent = await channel.SendOtpAsync(contact, code, ct);
        if (!sent)
        {
            cache.Remove($"{CodeCacheKeyPrefix}:{contact}"); // không gửi được → không để code sống
            return new OtpSendResult(false, _resendCooldownSeconds);
        }

        cache.Set(lastSentKey, DateTime.UtcNow, TimeSpan.FromSeconds(Math.Max(_resendCooldownSeconds, 1)));
        cache.Set(dayKey, sentToday + 1, TimeSpan.FromHours(26));
        return new OtpSendResult(true, _resendCooldownSeconds);
    }

    public Task<OtpVerifyResult> VerifyOtpAsync(string contact, string otp, CancellationToken ct = default)
    {
        var lockKey = $"{CodeCacheKeyPrefix}-lock:{contact}";
        if (cache.TryGetValue(lockKey, out DateTime lockUntil))
        {
            var remaining = (int)(lockUntil - DateTime.UtcNow).TotalMinutes;
            return Task.FromResult(new OtpVerifyResult(false, null, Math.Max(remaining, 1),
                "Quá nhiều lần thử sai. Vui lòng thử lại sau."));
        }

        var codeKey = $"{CodeCacheKeyPrefix}:{contact}";
        if (cache.TryGetValue(codeKey, out string? stored) && stored == otp)
        {
            cache.Remove(codeKey);
            cache.Remove($"{CodeCacheKeyPrefix}-attempt:{contact}");

            // Session token single-use — purpose + contact bind
            var token = Guid.NewGuid().ToString("N");
            cache.Set($"{CodeCacheKeyPrefix}-session:{token}",
                new OtpSession(contact, Purpose, DateTime.UtcNow.AddMinutes(SessionTtlMinutes)),
                TimeSpan.FromMinutes(SessionTtlMinutes));
            return Task.FromResult(new OtpVerifyResult(true, token, null, null));
        }

        // sai — đếm attempt
        var attemptKey = $"{CodeCacheKeyPrefix}-attempt:{contact}";
        var attempts = cache.TryGetValue(attemptKey, out int current) ? current : 0;
        attempts++;
        if (attempts >= _maxAttempts)
        {
            cache.Set(lockKey, DateTime.UtcNow.AddHours(_lockoutHours), TimeSpan.FromHours(_lockoutHours));
            cache.Remove(codeKey);
            cache.Remove(attemptKey);
            logger.LogWarning("OTP locked — {Contact} sau {Attempts} lần sai", contact, attempts);
            return Task.FromResult(new OtpVerifyResult(false, null, _lockoutHours * 60,
                "Quá nhiều lần thử sai. Vui lòng thử lại sau."));
        }
        cache.Set(attemptKey, attempts, TimeSpan.FromMinutes(CodeTtlMinutes));
        return Task.FromResult(new OtpVerifyResult(false, null, null, "Mã không đúng hoặc đã hết hạn."));
    }

    public Task<OtpSession?> ConsumeSessionAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Task.FromResult<OtpSession?>(null);

        var key = $"{CodeCacheKeyPrefix}-session:{token}";
        if (cache.TryGetValue(key, out OtpSession? session))
        {
            cache.Remove(key); // single-use — consume luôn
            if (session is not null
                && session.Purpose == Purpose
                && session.ExpiresAtUtc > DateTime.UtcNow)
                return Task.FromResult<OtpSession?>(session);
        }
        return Task.FromResult<OtpSession?>(null);
    }
}