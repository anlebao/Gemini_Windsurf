using Microsoft.AspNetCore.DataProtection;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// W17-T1: Customer token generation and validation using IDataProtector.
    /// Token format: "{customerId}:{expiry:O}" — protected, 30-day TTL.
    /// </summary>
    public interface ICustomerTokenService
    {
        string CreateToken(Guid customerId);
        Guid? ValidateToken(string token);
    }

    public class CustomerTokenService(IDataProtectionProvider dataProtection) : ICustomerTokenService
    {
        private readonly IDataProtector _protector = dataProtection.CreateProtector("CustomerToken.v1");
        private const int TokenDaysValid = 30;

        public string CreateToken(Guid customerId)
        {
            var expiry = DateTimeOffset.UtcNow.AddDays(TokenDaysValid);
            var payload = $"{customerId}:{expiry:O}";
            return _protector.Protect(payload);
        }

        public Guid? ValidateToken(string token)
        {
            try
            {
                var payload = _protector.Unprotect(token);
                var parts = payload.Split(':');
                if (parts.Length < 2) return null;

                if (!Guid.TryParse(parts[0], out var customerId)) return null;

                // Re-join remaining parts to reconstruct the expiry (ISO-8601 contains ':')
                var expiryStr = string.Join(":", parts.Skip(1));
                if (!DateTimeOffset.TryParse(expiryStr, out var expiry)) return null;
                if (expiry < DateTimeOffset.UtcNow) return null;

                return customerId;
            }
            catch
            {
                return null;
            }
        }
    }
}
