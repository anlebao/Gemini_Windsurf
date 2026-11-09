using System.Net;
using System.Net.Http.Json;
using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// Self-Serve Merchant Onboarding P1 (2026-11-09 — task_p1_khachlink_wizard):
/// HTTP client cho /onboard wizard trên KhachLink WASM.
/// Gọi Gateway public endpoints (P0 contract — MerchantSignupController +
/// PlatformOnboardingFlagsController). KHÔNG tham chiếu CoreHub — DTO mirror
/// contract, serialize camelCase (server JsonStringEnumConverter đọc cả số).
/// </summary>
public class MerchantSignupHttpService(IHttpClientFactory httpClientFactory, ILogger<MerchantSignupHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<MerchantSignupHttpService> _logger = logger;

    /// <summary>4 toggles sysadmin (FR-22) — public GET, cache phía client (sessionStorage).</summary>
    public async Task<OnboardingFlagsInfo?> GetOnboardingFlagsAsync(CancellationToken ct = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<OnboardingFlagsInfo>("api/v1/platform/onboarding-flags", ct);
        }
        catch (Exception ex)
        {
            // Fail-open: không đọc được flags → coi như mở (server vẫn enforce master switch khi submit).
            _logger.LogWarning(ex, "Onboarding flags fetch failed — default open");
            return null;
        }
    }

    /// <summary>Submit self-serve signup → Pending tenant. Response generic (anti-enumeration G10).</summary>
    public async Task<MerchantSignupSubmitOutcome> SubmitAsync(MerchantSignupSubmitRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/merchant-signups", request, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return MerchantSignupSubmitOutcome.Failed("Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau.");

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                var closed = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken: ct);
                return MerchantSignupSubmitOutcome.Failed(closed?.Error ?? "Đăng ký tự phục vụ tạm đóng.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken: ct);
                _logger.LogWarning("Merchant signup submit failed: {Status} {Error}", response.StatusCode, body?.Error);
                return MerchantSignupSubmitOutcome.Failed(body?.Error ?? $"Gửi đăng ký thất bại (HTTP {(int)response.StatusCode}).");
            }

            var result = await response.Content.ReadFromJsonAsync<MerchantSignupResultBody>(cancellationToken: ct);
            return MerchantSignupSubmitOutcome.Ok(result?.Message ?? "Đăng ký thành công!", result?.SignupId ?? Guid.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Merchant signup submit exception (case {Case})", request.Case);
            return MerchantSignupSubmitOutcome.Failed("Lỗi kết nối khi gửi đăng ký. Vui lòng thử lại.");
        }
    }

    /// <summary>Tra trạng thái đăng ký — cặp signupId + phone (anti-enumeration: sai → NotFound).</summary>
    public async Task<SignupStatusInfo?> GetStatusAsync(Guid signupId, string phone, CancellationToken ct = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<SignupStatusInfo>(
                $"api/v1/merchant-signups/status?signupId={signupId}&phone={Uri.EscapeDataString(phone)}", ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return new SignupStatusInfo("NotFound", null, "Vui lòng nhập đủ mã đăng ký và số điện thoại.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signup status lookup failed for {SignupId}", signupId);
            return null;
        }
    }

    /// <summary>
    /// P6 hook (FR-17): OCR cloud-assist khi confidence thấp. Endpoint P6 chưa tồn tại →
    /// trả null (404/501 tolerated) → wizard ẩn nút "Nhận diện nâng cao".
    /// </summary>
    public async Task<OcrAssistResultInfo?> OcrAssistAsync(string imageUrl, string kind, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/ocr-assist",
                new { imageUrl, kind }, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<OcrAssistResultInfo>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OCR assist unavailable ({Kind})", kind);
            return null;
        }
    }

    // ── Local DTOs mirror P0 contract (camelCase JSON) ─────────────────────

    private sealed record ErrorBody(string? Error);
    private sealed record MerchantSignupResultBody(string Message, Guid SignupId);
}

/// <summary>3 case self-serve — giá trị khớp VanAn.CoreHub.Services.Onboarding.SignupCase.</summary>
public enum MerchantSignupCase
{
    CompanyWithLicense = 0,      // Case A: có GPKD (MST bắt buộc)
    SmallShopNoLicense = 1,      // Case B: shop nhỏ không GPKD (HKDGroup bắt buộc)
    StreetVendorUnregistered = 2 // Case Rong: bán hàng rong chưa đăng ký (force HKD+Group1)
}

/// <summary>4 toggles onboarding (mirror OnboardingFlagsDto).</summary>
public sealed record OnboardingFlagsInfo(
    bool SelfServeEnabled,
    bool DirectoryCta,
    bool QrEntry,
    bool DeeplinkPrefill);

/// <summary>Một dòng sản phẩm từ menu OCR (mirror OcrProductLineDto).</summary>
public sealed record OcrProductLineInput(string Name, decimal? Price, int LineNo);

/// <summary>Thông tin GPKD (mirror OcrGpdkInfo) — chỉ Case A.</summary>
public sealed record GpdkInfoInput(
    string? Mst,
    string? Name,
    string? Address,
    string? LegalForm,
    string? Owner);

/// <summary>Request submit — mirror MerchantSignupRequest (PascalCase → camelCase khi serialize).</summary>
public sealed record MerchantSignupSubmitRequest(
    MerchantSignupCase Case,
    string Name,
    string? Address,
    BusinessType BusinessType,
    HKDGroup? HkdGroup,
    GpdkInfoInput? GpdkInfo,
    string? SignName,
    double? Latitude,
    double? Longitude,
    List<string>? PhotoUrls,
    List<OcrProductLineInput>? ProductLines,
    string ContactName,
    string ContactPhone,
    string? ContactEmail,
    string OwnerUsername,
    string OwnerPassword,
    string? TurnstileToken,
    string? HoneypotWebsite,
    string? Src = null);

/// <summary>Kết quả submit.</summary>
public sealed record MerchantSignupSubmitOutcome(bool Success, string? Message, Guid? SignupId, string? Error)
{
    public static MerchantSignupSubmitOutcome Ok(string message, Guid signupId) => new(true, message, signupId, null);
    public static MerchantSignupSubmitOutcome Failed(string error) => new(false, null, null, error);
}

/// <summary>Status lookup (mirror SignupStatusResult): Pending | Active | Rejected | NotFound.</summary>
public sealed record SignupStatusInfo(string Status, string? Slug, string? Message);

/// <summary>P6 OCR cloud-assist response (contract dự kiến — tolerant merge ở wizard).</summary>
public sealed record OcrAssistResultInfo(
    string? Mst, string? Name, string? Address, string? LegalForm, string? Owner,
    string? SignName, List<OcrProductLineInput>? ProductLines, double? Confidence);
