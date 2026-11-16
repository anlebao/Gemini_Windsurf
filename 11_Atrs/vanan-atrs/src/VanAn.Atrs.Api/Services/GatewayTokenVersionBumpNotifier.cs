using System.Net.Http.Json;

namespace VanAn.Atrs.Api.Services;

/// <summary>
/// ATRS-3 hook: recovery COMPLETED → POST Gateway internal /api/internal/auth/token-version/bump
/// (X-Internal-Api-Key) — revoke mọi token cũ của identity (DemoUser | Customer).
/// </summary>
public sealed class GatewayTokenVersionBumpNotifier(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    : VanAn.Atrs.Infra.ITokenVersionBumpNotifier
{
    public async Task BumpAsync(Guid userId, string identityType, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("gateway-internal");
        var response = await client.PostAsJsonAsync("/api/internal/auth/token-version/bump",
            new { userId, identityType }, ct);
        response.EnsureSuccessStatusCode();
    }
}