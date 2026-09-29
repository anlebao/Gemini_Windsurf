using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// Membership Infrastructure (2026-09-29): HTTP client cho Gateway membership endpoints.
/// - Tạo hồ sơ xin gia nhập HTX (cần X-Customer-Token — applicant = Vạn An Network ID).
/// - Ghi nhận consent (Điều lệ version) — evidence.
/// - Nộp hồ sơ (Draft → Submitted).
/// - Xem Điều lệ/version của HTX (public — không cần token).
/// </summary>
public class MembershipHttpService(IHttpClientFactory httpClientFactory, ILogger<MembershipHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<MembershipHttpService> _logger = logger;

    /// <summary>GET /api/membership/htx-profile/{htxTenantId} — Điều lệ + version (public).</summary>
    public async Task<HtxProfileDto?> GetHtxProfileAsync(Guid htxTenantId, CancellationToken ct = default)
    {
        try
        {
            var profile = await _httpClient.GetFromJsonAsync<HtxProfileDto>(
                $"api/membership/htx-profile/{htxTenantId}", cancellationToken: ct);
            return profile;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "GetHtxProfile failed for HTX {HtxId}", htxTenantId);
            return null;
        }
    }

    /// <summary>
    /// POST /api/membership/applications — tạo hồ sơ Draft (applicant từ token, IDOR-safe).
    /// POST /api/membership/consent — ghi nhận consent Điều lệ (evidence, append-only).
    /// Trả về (applicationId, error).
    /// </summary>
    public async Task<(Guid? ApplicationId, string? Error)> CreateApplicationAndConsentAsync(
        string customerToken,
        CreateMembershipApplicationRequestDto request,
        string charterVersion,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(customerToken))
            return (null, "Bạn cần đăng nhập trước khi đăng ký thành viên.");

        try
        {
            // 1. Consent Điều lệ trước (evidence — applicant phải xem Điều lệ TRƯỚC khi nộp — SRS §14)
            var consentBody = new
            {
                HtxTenantId = request.HtxTenantId,
                DocumentType = "Charter",
                DocumentVersion = charterVersion
            };
            var consentResponse = await PostWithTokenAsync(
                "api/membership/consent", consentBody, customerToken, ct);
            if (!consentResponse.IsSuccessStatusCode)
            {
                var consentError = await ReadErrorAsync(consentResponse, ct);
                return (null, consentError ?? "Không thể ghi nhận xác nhận Điều lệ.");
            }

            // 2. Tạo hồ sơ
            var appResponse = await PostWithTokenAsync(
                "api/membership/applications", request, customerToken, ct);

            if (appResponse.StatusCode == HttpStatusCode.Conflict)
            {
                var conflictError = await ReadErrorAsync(appResponse, ct);
                return (null, conflictError ?? "Bạn đã có hồ sơ đang xét duyệt cho HTX này.");
            }
            if (!appResponse.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(appResponse, ct);
                _logger.LogWarning("Create application failed: {Status} {Error}", appResponse.StatusCode, error);
                return (null, error ?? $"Tạo hồ sơ thất bại (HTTP {appResponse.StatusCode}).");
            }

            var result = await appResponse.Content.ReadFromJsonAsync<CreateApplicationResultBody>(cancellationToken: ct);
            _logger.LogInformation("Membership application {ApplicationId} created", result?.ApplicationId);
            return (result?.ApplicationId, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create application exception");
            return (null, "Lỗi kết nối khi tạo hồ sơ. Vui lòng thử lại.");
        }
    }

    /// <summary>POST /api/membership/applications/{id}/submit — nộp hồ sơ (Draft → Submitted).</summary>
    public async Task<(bool Success, string? Error)> SubmitApplicationAsync(
        string customerToken,
        Guid applicationId,
        CancellationToken ct = default)
    {
        try
        {
            var response = await PostWithTokenAsync(
                $"api/membership/applications/{applicationId}/submit", null, customerToken, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response, ct);
                return (false, error ?? $"Nộp hồ sơ thất bại (HTTP {response.StatusCode}).");
            }
            return (true, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Submit application exception");
            return (false, "Lỗi kết nối khi nộp hồ sơ. Vui lòng thử lại.");
        }
    }

    /// <summary>
    /// POST /api/membership/applications/{id}/documents — đính kèm tài liệu xác nhận
    /// (chữ ký online / scan form giấy) vào hồ sơ (SRS §17.2). Applicant sở hữu hồ sơ.
    /// </summary>
    public async Task<(bool Success, string? Error)> AttachDocumentAsync(
        string customerToken,
        Guid applicationId,
        string documentType,
        string documentVersion,
        string storageReference,
        string? hash = null,
        CancellationToken ct = default)
    {
        try
        {
            var body = new AttachDocumentRequestDto(documentType, documentVersion, storageReference, hash);
            var response = await PostWithTokenAsync(
                $"api/membership/applications/{applicationId}/documents", body, customerToken, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response, ct);
                return (false, error ?? $"Đính kèm tài liệu thất bại (HTTP {response.StatusCode}).");
            }
            return (true, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Attach document exception");
            return (false, "Lỗi kết nối khi đính kèm tài liệu. Vui lòng thử lại.");
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> PostWithTokenAsync(
        string uri, object? body, string customerToken, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Add("X-Customer-Token", customerToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await _httpClient.SendAsync(request, ct);
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken: ct);
            return body?.Error;
        }
        catch
        {
            return null;
        }
    }

    // ── Local DTOs ──────────────────────────────────────────────────────────

    private sealed record CreateApplicationResultBody(Guid? ApplicationId);
    private sealed record ErrorBody(string? Error);
}

/// <summary>Request DTO mirrors CoreHub CreateMembershipApplicationRequest (camelCase enums).</summary>
public sealed record CreateMembershipApplicationRequestDto(
    Guid HtxTenantId,
    string MembershipType,
    string FullName,
    string PhoneNumber,
    string? Email,
    string? Region,
    string ExpectedRole,
    Guid? BusinessTenantId = null,
    string? CharterVersion = null,
    string? ConsentVersion = null);

/// <summary>HTX profile DTO (charter version + url — public, no PII).</summary>
public sealed record HtxProfileDto(
    Guid Id,
    Guid HtxTenantId,
    string CharterVersion,
    string TermsVersion,
    string? CharterUrl);

/// <summary>Request DTO mirrors AttachMembershipDocumentRequest.</summary>
public sealed record AttachDocumentRequestDto(
    string DocumentType,
    string DocumentVersion,
    string StorageReference,
    string? Hash = null);
