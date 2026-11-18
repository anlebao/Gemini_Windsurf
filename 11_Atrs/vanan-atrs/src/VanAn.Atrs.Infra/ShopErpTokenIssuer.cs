using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using VanAn.Atrs.Contracts;

namespace VanAn.Atrs.Infra;

/// <summary>
/// ATRS-21 — atrs → ShopERP customer token issuance qua named HttpClient "shoperp"
/// (Atrs:ShopErpBaseUrl). Auth: X-Internal-Api-Key = Atrs:ShopErpInternalApiKey (= InternalLoyalty:ApiKey ShopERP).
/// Mọi lỗi mạng/HTTP → PasskeyTokenResult fail (caller map sang ReasonCode generic).
/// </summary>
public sealed class ShopErpTokenIssuer(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    : IPasskeyLoginTokenIssuer
{
    private const string ClientName = "shoperp";
    private const string ApiKeyHeader = "X-Internal-Api-Key";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public async Task<PasskeyTokenResult> IssueByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
    {
        var response = await PostAsync("api/internal/customer-token/by-id", new { customerId }, ct);
        return await ReadResultAsync(response, ct);
    }

    public async Task<PasskeyTokenResult> IssueByDeviceAsync(string deviceId, Guid? tenantId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(deviceId, out var deviceGuid))
            return new PasskeyTokenResult(false, "InvalidDeviceId", null, null, false);

        var response = await PostAsync("api/internal/customer-token/by-device",
            new { deviceId = deviceGuid, tenantId }, ct);
        return await ReadResultAsync(response, ct);
    }

    private async Task<HttpResponseMessage?> PostAsync(string path, object body, CancellationToken ct)
    {
        var apiKey = configuration["Atrs:ShopErpInternalApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return null; // chưa wire config → fail graceful, không crash login

        try
        {
            var client = httpClientFactory.CreateClient(ClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.Add(ApiKeyHeader, apiKey);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);
            return await client.SendAsync(request, cts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task<PasskeyTokenResult> ReadResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
            return new PasskeyTokenResult(false, "TokenIssuanceFailed", null, null, false);

        try
        {
            if (!response.IsSuccessStatusCode)
                return new PasskeyTokenResult(false, "TokenIssuanceFailed", null, null, false);

            var dto = await response.Content.ReadFromJsonAsync<ShopErpTokenResponse>(cancellationToken: ct);
            if (dto?.CustomerToken is null || dto.CustomerId == Guid.Empty)
                return new PasskeyTokenResult(false, "TokenIssuanceFailed", null, null, false);

            return new PasskeyTokenResult(true, null, dto.CustomerToken, dto.CustomerId, dto.Created);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new PasskeyTokenResult(false, "TokenIssuanceFailed", null, null, false);
        }
    }

    private sealed record ShopErpTokenResponse(
        [property: JsonPropertyName("customerToken")] string? CustomerToken,
        [property: JsonPropertyName("customerId")] Guid CustomerId,
        [property: JsonPropertyName("created")] bool Created);
}
