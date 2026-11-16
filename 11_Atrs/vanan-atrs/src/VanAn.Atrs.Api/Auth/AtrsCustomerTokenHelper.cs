using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VanAn.Atrs.Api.Auth;

/// <summary>
/// ATRS customer auth — validate X-Customer-Token bằng cách forward tới ShopERP
/// /api/customer-identity/me (pattern Gateway CustomerTokenValidationHelper).
/// Phân biệt token invalid (401/403) vs ShopERP down (5xx/timeout) → 401 vs 503.
/// </summary>
public enum AtrsCustomerTokenStatus
{
    Valid,
    InvalidToken,
    DownstreamUnavailable
}

public sealed record AtrsCustomerAuthResult(AtrsCustomerTokenStatus Status, Guid? CustomerId, Guid? TrustSubjectId);

public static class AtrsCustomerTokenHelper
{
    private sealed class MeResponse
    {
        public Guid? CustomerId { get; set; }
    }

    public static async Task<(AtrsCustomerTokenStatus Status, Guid? CustomerId)> ValidateTokenAsync(
        IHttpClientFactory httpClientFactory, string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token))
            return (AtrsCustomerTokenStatus.InvalidToken, null);

        try
        {
            var client = httpClientFactory.CreateClient("shoperp");
            var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/customer-identity/me");
            meReq.Headers.Add("X-Customer-Token", token);

            var meResp = await client.SendAsync(meReq, ct);
            if (meResp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return (AtrsCustomerTokenStatus.InvalidToken, null);

            if (!meResp.IsSuccessStatusCode)
                return (AtrsCustomerTokenStatus.DownstreamUnavailable, null); // 5xx — ShopERP down

            var meContent = await meResp.Content.ReadFromJsonAsync<MeResponse>(ct);
            if (meContent?.CustomerId is null || meContent.CustomerId == Guid.Empty)
                return (AtrsCustomerTokenStatus.InvalidToken, null);

            return (AtrsCustomerTokenStatus.Valid, meContent.CustomerId.Value);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return (AtrsCustomerTokenStatus.DownstreamUnavailable, null);
        }
    }
}