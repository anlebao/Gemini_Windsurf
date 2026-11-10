using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OtpNet;
using VanAn.CoreHub.Infrastructure;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;

namespace VanAn.CoreHub.Services;

/// <summary>MFA-1 (2026-11-10, user duyệt): Admin MFA — TOTP + trusted device + email OTP fallback.</summary>
public enum MfaVerifyResult
{
    Success = 0,
    NotEnabled = 1,
    Invalid = 2,
    Locked = 3
}

/// <summary>MFA-1: setup payload — secret base32 + otpauth URI (QR).</summary>
public record MfaSetupResult(string Secret, string OtpauthUri);

public interface IMfaService
{
    /// <summary>Sinh secret TOTP + otpauth:// URI cho QR (KHÔNG lưu — EnableAsync mới lưu sau khi verify code).</summary>
    MfaSetupResult GenerateSetup(string userId, string username);

    /// <summary>Verify code với secret vừa generate rồi mới EnableMfa (lưu secret).</summary>
    Task<bool> EnableAsync(string userId, string secret, string code, CancellationToken ct = default);

    /// <summary>Tắt MFA (caller đã xác nhận password ở API layer).</summary>
    Task DisableAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Login step 2: trusted token hợp lệ → Success (skip MFA); MfaEnabled=false → NotEnabled;
    /// sai code ≥ MaxAttempts → Locked; đúng → Success.
    /// </summary>
    Task<MfaVerifyResult> VerifyLoginAsync(string userId, string? code, string? trustedToken, CancellationToken ct = default);

    /// <summary>Sinh raw token (trả về để set cookie) + lưu SHA-256 hash lên user (1 device MVP).</summary>
    Task<string> IssueTrustedTokenAsync(string userId, CancellationToken ct = default);

    /// <summary>Token raw từ cookie → so hash với user.TrustedDeviceHash.</summary>
    Task<bool> IsTrustedAsync(string userId, string? trustedToken, CancellationToken ct = default);

    /// <summary>Xóa trusted device ("quên thiết bị này" / logout).</summary>
    Task RevokeTrustAsync(string userId, CancellationToken ct = default);

    /// <summary>Gửi email OTP fallback (Brevo, 0đ) khi mất authenticator. Trả true nếu đã gửi.</summary>
    Task<bool> SendEmailFallbackOtpAsync(string userId, CancellationToken ct = default);

    /// <summary>Verify email OTP — single-use.</summary>
    Task<bool> VerifyEmailFallbackOtpAsync(string userId, string code);
}

public class MfaService(
    IVanAnDbContext dbContext,
    IMemoryCache cache,
    IConfiguration configuration,
    IEmailService emailService,
    ILogger<MfaService> logger) : IMfaService
{
    private const string Issuer = "VanAn";
    private readonly int _maxAttempts = configuration.GetValue("Mfa:MaxAttempts", 5);
    private readonly int _lockoutMinutes = configuration.GetValue("Mfa:LockoutMinutes", 15);
    private const int EmailOtpTtlMinutes = 10;

    public MfaSetupResult GenerateSetup(string userId, string username)
    {
        var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
        var uri = $"otpauth://totp/{Issuer}:{Uri.EscapeDataString(username)}?secret={secret}&issuer={Issuer}";
        return new MfaSetupResult(secret, uri);
    }

    public async Task<bool> EnableAsync(string userId, string secret, string code, CancellationToken ct = default)
    {
        var user = await LoadUserAsync(userId, ct) ?? throw new KeyNotFoundException($"User {userId} not found.");
        if (user.MfaEnabled)
            throw new InvalidOperationException("MFA đã được bật.");

        var totp = new Totp(Base32Encoding.ToBytes(secret));
        if (!totp.VerifyTotp(code, out _, new VerificationWindow(2, 2)))
            return false;

        user.EnableMfa(secret);
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("MFA-1: enabled MFA for user {UserId}", userId);
        return true;
    }

    public async Task DisableAsync(string userId, CancellationToken ct = default)
    {
        var user = await LoadUserAsync(userId, ct) ?? throw new KeyNotFoundException($"User {userId} not found.");
        user.DisableMfa();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("MFA-1: disabled MFA for user {UserId}", userId);
    }

    public async Task<MfaVerifyResult> VerifyLoginAsync(string userId, string? code, string? trustedToken, CancellationToken ct = default)
    {
        var user = await LoadUserAsync(userId, ct) ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (!user.MfaEnabled)
            return MfaVerifyResult.NotEnabled;

        // Trusted device cookie → skip MFA
        if (await IsTrustedAsync(userId, trustedToken, ct))
            return MfaVerifyResult.Success;

        var lockKey = GetLockKey(userId);
        if (cache.TryGetValue(lockKey, out _))
            return MfaVerifyResult.Locked;

        if (string.IsNullOrWhiteSpace(code) || user.MfaSecret is null)
            return MfaVerifyResult.Invalid;

        var totp = new Totp(Base32Encoding.ToBytes(user.MfaSecret));
        if (totp.VerifyTotp(code, out _, new VerificationWindow(2, 2)))
        {
            cache.Remove(GetAttemptKey(userId));
            return MfaVerifyResult.Success;
        }

        // sai — đếm attempt, đủ max → lock
        var attemptKey = GetAttemptKey(userId);
        var attempts = cache.TryGetValue(attemptKey, out int current) ? current : 0;
        attempts++;
        if (attempts >= _maxAttempts)
        {
            cache.Set(lockKey, true, TimeSpan.FromMinutes(_lockoutMinutes));
            cache.Remove(attemptKey);
            logger.LogWarning("MFA-1: user {UserId} locked out — {Attempts} failed attempts", userId, attempts);
            return MfaVerifyResult.Locked;
        }
        cache.Set(attemptKey, attempts, TimeSpan.FromMinutes(_lockoutMinutes));
        return MfaVerifyResult.Invalid;
    }

    public async Task<string> IssueTrustedTokenAsync(string userId, CancellationToken ct = default)
    {
        var user = await LoadUserAsync(userId, ct) ?? throw new KeyNotFoundException($"User {userId} not found.");
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        user.SetTrustedDevice(HashToken(token));
        await dbContext.SaveChangesAsync(ct);
        return token;
    }

    public async Task<bool> IsTrustedAsync(string userId, string? trustedToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(trustedToken))
            return false;
        var user = await LoadUserAsync(userId, ct);
        return user is not null && user.TrustedDeviceHash is not null
            && string.Equals(user.TrustedDeviceHash, HashToken(trustedToken), StringComparison.Ordinal);
    }

    public async Task RevokeTrustAsync(string userId, CancellationToken ct = default)
    {
        var user = await LoadUserAsync(userId, ct) ?? throw new KeyNotFoundException($"User {userId} not found.");
        user.ClearTrustedDevice();
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> SendEmailFallbackOtpAsync(string userId, CancellationToken ct = default)
    {
        var user = await LoadUserAsync(userId, ct) ?? throw new KeyNotFoundException($"User {userId} not found.");
        if (string.IsNullOrWhiteSpace(user.Email))
            return false;

        var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
        cache.Set(GetEmailOtpKey(userId), code, TimeSpan.FromMinutes(EmailOtpTtlMinutes));

        var sent = await emailService.SendEmailAsync(
            user.Email,
            "Mã xác minh đăng nhập Vạn An",
            $"<p>Mã xác minh của bạn: <b style=\"font-size:24px\">{code}</b></p><p>Mã có hiệu lực {EmailOtpTtlMinutes} phút.</p>",
            ct);
        if (!sent)
        {
            cache.Remove(GetEmailOtpKey(userId)); // không gửi được → không để code sống
            logger.LogWarning("MFA-1: email fallback OTP không gửi được cho user {UserId}", userId);
        }
        return sent;
    }

    public Task<bool> VerifyEmailFallbackOtpAsync(string userId, string code)
    {
        var key = GetEmailOtpKey(userId);
        if (cache.TryGetValue(key, out string? stored) && stored == code)
        {
            cache.Remove(key); // single-use
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    private async Task<DemoUser?> LoadUserAsync(string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var id)) return null;
        return await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private static string GetAttemptKey(string userId) => $"mfa-attempt:{userId}";
    private static string GetLockKey(string userId) => $"mfa-lock:{userId}";
    private static string GetEmailOtpKey(string userId) => $"mfa-email-otp:{userId}";
}