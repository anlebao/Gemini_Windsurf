using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    public record SocialUserInfo(string Email, string FullName, string? PictureUrl, string Provider);

    public record SocialAuthResult(Customer Customer, string CustomerToken, bool IsNewCustomer);

    public interface IGoogleAuthService
    {
        string GetAuthorizationUrl(string redirectUri, string? state = null);
        Task<SocialUserInfo?> ExchangeCodeForUserInfoAsync(string code, string redirectUri);
    }
}
