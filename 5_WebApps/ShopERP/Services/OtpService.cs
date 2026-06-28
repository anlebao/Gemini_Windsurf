using Microsoft.Extensions.Caching.Memory;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// W17-T1: OTP generation and verification.
    /// Stores OTP in IMemoryCache with 5-minute TTL. No DB dependency.
    /// </summary>
    public interface IOtpService
    {
        string GenerateAndStoreOtp(string phoneNumber);
        bool VerifyOtp(string phoneNumber, string otp);
    }

    public class OtpService(IMemoryCache cache, IConfiguration config) : IOtpService
    {
        private const int OtpTtlMinutes = 5;
        private static readonly Random _rng = new();

        public string GenerateAndStoreOtp(string phoneNumber)
        {
            var otp = _rng.Next(100000, 999999).ToString();
            var cacheKey = GetCacheKey(phoneNumber);
            cache.Set(cacheKey, otp, TimeSpan.FromMinutes(OtpTtlMinutes));
            return otp;
        }

        public bool VerifyOtp(string phoneNumber, string otp)
        {
            var cacheKey = GetCacheKey(phoneNumber);
            if (cache.TryGetValue(cacheKey, out string? storedOtp) && storedOtp == otp)
            {
                cache.Remove(cacheKey);
                return true;
            }
            return false;
        }

        private static string GetCacheKey(string phoneNumber) => $"otp:{phoneNumber}";
    }
}
