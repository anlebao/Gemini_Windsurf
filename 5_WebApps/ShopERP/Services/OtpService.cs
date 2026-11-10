using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// W17-T1: OTP generation and verification.
    /// Stores OTP in IMemoryCache with 5-minute TTL. No DB dependency.
    /// W0 hardening (2026-11-10, REVIEW B1 — user duyệt):
    /// - Random → RandomNumberGenerator (CSPRNG)
    /// - attempt counter per purpose/contact — max sai → lock (chống brute-force)
    /// - purpose binding: key namespace `otp:{purpose}:{contact}` (login ≠ upgrade)
    /// </summary>
    public interface IOtpService
    {
        string GenerateAndStoreOtp(string phoneNumber, string purpose = "customer-login");
        bool VerifyOtp(string phoneNumber, string otp, string purpose = "customer-login");
    }

    public class OtpService(IMemoryCache cache, IConfiguration config) : IOtpService
    {
        private const string DefaultPurpose = "customer-login";
        private readonly int _ttlMinutes = config.GetValue("Otp:TtlMinutes", 5);
        private readonly int _maxAttempts = config.GetValue("Otp:MaxAttempts", 5);
        private readonly int _lockoutMinutes = config.GetValue("Otp:LockoutMinutes", 5);

        public string GenerateAndStoreOtp(string phoneNumber, string purpose = DefaultPurpose)
        {
            // CSPRNG — KHÔNG dùng Random (predictable + seed-based)
            var otp = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
            cache.Set(GetOtpKey(purpose, phoneNumber), otp, TimeSpan.FromMinutes(_ttlMinutes));
            cache.Remove(GetAttemptKey(purpose, phoneNumber));
            cache.Remove(GetLockKey(purpose, phoneNumber));
            return otp;
        }

        public bool VerifyOtp(string phoneNumber, string otp, string purpose = DefaultPurpose)
        {
            var lockKey = GetLockKey(purpose, phoneNumber);
            if (cache.TryGetValue(lockKey, out _))
                return false; // locked — kể cả code đúng

            var otpKey = GetOtpKey(purpose, phoneNumber);
            if (cache.TryGetValue(otpKey, out string? storedOtp) && storedOtp == otp)
            {
                cache.Remove(otpKey);   // single-use
                cache.Remove(GetAttemptKey(purpose, phoneNumber));
                return true;
            }

            // sai (hoặc hết hạn) — đếm attempt
            var attemptKey = GetAttemptKey(purpose, phoneNumber);
            var attempts = cache.TryGetValue(attemptKey, out int current) ? current : 0;
            attempts++;
            if (attempts >= _maxAttempts)
            {
                cache.Set(lockKey, true, TimeSpan.FromMinutes(_lockoutMinutes));
                cache.Remove(otpKey);   // burn OTP khi lock
                cache.Remove(attemptKey);
            }
            else
            {
                cache.Set(attemptKey, attempts, TimeSpan.FromMinutes(_ttlMinutes));
            }
            return false;
        }

        private static string GetOtpKey(string purpose, string phoneNumber) => $"otp:{purpose}:{phoneNumber}";
        private static string GetAttemptKey(string purpose, string phoneNumber) => $"otp-attempt:{purpose}:{phoneNumber}";
        private static string GetLockKey(string purpose, string phoneNumber) => $"otp-lock:{purpose}:{phoneNumber}";
    }
}