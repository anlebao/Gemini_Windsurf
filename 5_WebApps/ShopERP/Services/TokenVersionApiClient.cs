using System.Text.Json;

namespace VanAn.ShopERP.Services;

/// <summary>
/// ATRS-3 (D3): đọc token version từ Gateway (PG — source of truth) để đưa claim `tv`
/// vào JWT lúc login. Fail-open: Gateway không trả lời → version 0 (không chặn login;
/// sau bump token cũ bị Gateway từ chối và user phải login lại khi Gateway khỏe).
/// </summary>
public interface ITokenVersionApiClient
{
    Task<int> GetTokenVersionAsync(Guid userId, CancellationToken ct = default);
}

public sealed class TokenVersionApiClient(IHttpClientFactory httpClientFactory, IConfiguration configuration) : ITokenVersionApiClient
{
    public async Task<int> GetTokenVersionAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var baseUrl = configuration["Gateway:BaseUrl"] ?? "http://gateway:5001";
            var apiKey = configuration["InternalLoyalty:ApiKey"] ?? "";
            var client = httpClientFactory.CreateClient("GatewayInternal");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/internal/auth/token-version/{userId}");
            request.Headers.TryAddWithoutValidation("X-Internal-Api-Key", apiKey);
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return 0;
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return json.TryGetProperty("version", out var version) ? version.GetInt32() : 0;
        }
        catch
        {
            return 0; // fail-open
        }
    }
}