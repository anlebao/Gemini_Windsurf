using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    public record SocialUserInfo(string Email, string FullName, string? PictureUrl, string Provider);

    public record SocialAuthResult(Customer Customer, string CustomerToken, bool IsNewCustomer);

    public record GoogleAuthError(string Reason, string? Details);

    public class GoogleAuthResponse
    {
        public SocialUserInfo? UserInfo { get; set; }
        public GoogleAuthError? Error { get; set; }
        public bool Success => UserInfo != null;
    }

    public interface IGoogleAuthService
    {
        string GetAuthorizationUrl(string redirectUri, string? state = null);
        Task<GoogleAuthResponse> ExchangeCodeForUserInfoAsync(string code, string redirectUri);
    }
}
