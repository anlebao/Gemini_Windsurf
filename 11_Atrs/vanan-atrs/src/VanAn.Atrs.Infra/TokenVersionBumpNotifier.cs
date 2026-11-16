namespace VanAn.Atrs.Infra;

/// <summary>
/// ATRS-3 hook: sau recovery COMPLETED → bump token version (revoke token cũ).
/// Implementation HTTP (gọi Gateway internal POST /api/internal/auth/token-version/bump)
/// nằm ở Api project; mặc định Noop khi chưa cấu hình GatewayBaseUrl.
/// </summary>
public interface ITokenVersionBumpNotifier
{
    Task BumpAsync(Guid userId, string identityType, CancellationToken ct = default);
}

public sealed class NoopTokenVersionBumpNotifier : ITokenVersionBumpNotifier
{
    public Task BumpAsync(Guid userId, string identityType, CancellationToken ct = default) => Task.CompletedTask;
}