namespace VanAn.CoreHub.Services;

public record PlatformLoginResult(Guid UserId, string Email, string Role, string Token);

public interface IPlatformUserLoginService
{
    Task<PlatformLoginResult?> LoginAsync(string username, string password, CancellationToken ct = default);
}
