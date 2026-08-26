using System.Net.Http.Json;
using System.Net;
using System.Text.Json.Serialization;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// Crawl-to-Onboard Phase 6 (2026-08-26): HTTP client for tenant claim submission.
/// KhachLink Claim.razor form calls Gateway POST /api/v1/tenants/{tenantId}/claims.
/// Gateway endpoint is [AllowAnonymous] + rate-limited (3/24h per IP — policy "claim-submit").
/// </summary>
public class ClaimHttpService(IHttpClientFactory httpClientFactory, ILogger<ClaimHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<ClaimHttpService> _logger = logger;

    /// <summary>
    /// Submit a claim for a Pending tenant. Returns success message, or error on failure.
    /// Handles 429 (rate limit) + 409 (conflict — already verified) + 404 (tenant not found).
    /// </summary>
    public async Task<ClaimSubmitOutcome> SubmitClaimAsync(
        Guid tenantId,
        SubmitClaimRequestDto request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return ClaimSubmitOutcome.Failed("Thông tin doanh nghiệp không hợp lệ.");

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/v1/tenants/{tenantId}/claims", request, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Claim submit rate-limited for tenant {TenantId}", tenantId);
                return ClaimSubmitOutcome.Failed(
                    "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau 24 giờ.");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                var body404 = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken: ct);
                return ClaimSubmitOutcome.Failed(body404?.Error ?? "Không tìm thấy doanh nghiệp.");
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                var body409 = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken: ct);
                return ClaimSubmitOutcome.Failed(body409?.Error ?? "Doanh nghiệp đã được xác thực.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken: ct);
                _logger.LogWarning("Claim submit failed: {Status} {Error}", response.StatusCode, body?.Error);
                return ClaimSubmitOutcome.Failed(body?.Error ?? $"Gửi yêu cầu thất bại (HTTP {response.StatusCode}).");
            }

            var result = await response.Content.ReadFromJsonAsync<ClaimResultBody>(cancellationToken: ct);
            _logger.LogInformation("Claim submitted for tenant {TenantId} — claim {ClaimId}",
                tenantId, result?.ClaimId);
            return ClaimSubmitOutcome.Ok(result?.Message ?? "Cảm ơn! Yêu cầu xác nhận đã gửi.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claim submit exception for tenant {TenantId}", tenantId);
            return ClaimSubmitOutcome.Failed("Lỗi kết nối khi gửi yêu cầu. Vui lòng thử lại.");
        }
    }

    // ── Local DTOs matching Gateway request/response bodies ──────────────────
    private sealed record ClaimResultBody(Guid ClaimId, string Message);
    private sealed record ErrorBody(string? Error);
}

/// <summary>Request DTO sent to Gateway — mirrors CoreHub SubmitClaimRequest.</summary>
public sealed record SubmitClaimRequestDto(
    string ClaimantName,
    string ClaimantPhone,
    string? ClaimantEmail,
    string GpkdImageUrl,
    string TaxCodeSubmitted);

/// <summary>Outcome of a claim submission — Ok (with message) or Failed (with error).</summary>
public sealed record ClaimSubmitOutcome(bool Success, string? Message, string? Error)
{
    public static ClaimSubmitOutcome Ok(string message) => new(true, message, null);
    public static ClaimSubmitOutcome Failed(string error) => new(false, null, error);
}
