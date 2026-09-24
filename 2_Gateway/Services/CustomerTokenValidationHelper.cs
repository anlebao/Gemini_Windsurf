using System.Net.Http.Json;

namespace VanAn.Gateway.Services;

/// <summary>
/// 2026-09-24 (deploy-window root cause): shared validation of the KhachLink
/// <c>X-Customer-Token</c> by forwarding to ShopERP <c>/api/customer-identity/me</c>.
///
/// Distinguishes a genuinely invalid token (ShopERP 401/403) from ShopERP being
/// temporarily unavailable (5xx / connection refused / timeout), so controllers can
/// return 401 vs 503 instead of misreporting a deploy window as an expired session.
/// </summary>
public enum CustomerTokenValidationStatus
{
    /// <summary>ShopERP validated the token — CustomerId is populated.</summary>
    Valid,

    /// <summary>ShopERP explicitly rejected the token (401/403) or returned no customer.</summary>
    InvalidToken,

    /// <summary>ShopERP unreachable or errored (5xx / network) — retryable, NOT a token problem.</summary>
    DownstreamUnavailable
}

public static class CustomerTokenValidationHelper
{
    private sealed class MeResponse
    {
        public Guid? CustomerId { get; set; }
    }

    public static async Task<(CustomerTokenValidationStatus Status, Guid? CustomerId)> ValidateAsync(
        IHttpClientFactory httpClientFactory,
        string? token,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token))
            return (CustomerTokenValidationStatus.InvalidToken, null);

        try
        {
            var client = httpClientFactory.CreateClient("shoperp");
            var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/customer-identity/me");
            meReq.Headers.Add("X-Customer-Token", token);

            var meResp = await client.SendAsync(meReq, ct);
            if (meResp.StatusCode == System.Net.HttpStatusCode.Unauthorized
                || meResp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return (CustomerTokenValidationStatus.InvalidToken, null);
            }

            if (!meResp.IsSuccessStatusCode)
            {
                // 5xx (502/503/504 from ShopERP's nginx, or 500) → ShopERP down, not a token problem.
                return (CustomerTokenValidationStatus.DownstreamUnavailable, null);
            }

            var meContent = await meResp.Content.ReadFromJsonAsync<MeResponse>(ct);
            if (meContent?.CustomerId == null || meContent.CustomerId == Guid.Empty)
                return (CustomerTokenValidationStatus.InvalidToken, null);

            return (CustomerTokenValidationStatus.Valid, meContent.CustomerId.Value);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            // Connection refused / timeout / malformed response → ShopERP unreachable, retryable.
            return (CustomerTokenValidationStatus.DownstreamUnavailable, null);
        }
    }
}
