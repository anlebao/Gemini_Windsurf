using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// ATRS-11 subset (Wave 2): Security center HTTP client — gọi Gateway
/// /api/v1/account-trust/* (YARP → vanan-atrs). KhachLink HTTP-only — không DB local.
/// Mọi method cần X-Customer-Token; hành động nhạy cảm (revoke/regenerate) cần X-Step-Up-Token.
/// </summary>
public class SecurityHttpService(IHttpClientFactory httpClientFactory, ILogger<SecurityHttpService> logger)
{
    private readonly HttpClient _http = httpClientFactory.CreateClient("gateway");
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private HttpRequestMessage Create(string method, string path, string customerToken, string? stepUpToken = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), $"api/v1/account-trust/{path}");
        request.Headers.Add("X-Customer-Token", customerToken);
        if (!string.IsNullOrEmpty(stepUpToken))
            request.Headers.Add("X-Step-Up-Token", stepUpToken);
        return request;
    }

    public async Task<TrustProfileSummaryDto?> GetMeAsync(string customerToken, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("GET", "me", customerToken), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<TrustProfileSummaryDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS me failed");
            return null;
        }
    }

    public async Task<RecoveryCodesStatusDto?> GetRecoveryCodesStatusAsync(string customerToken, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("GET", "recovery-codes/status", customerToken), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<RecoveryCodesStatusDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS recovery status failed");
            return null;
        }
    }

    public async Task<GenerateRecoveryCodesResultDto?> GenerateRecoveryCodesAsync(string customerToken, string stepUpToken, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("POST", "recovery-codes/generate", customerToken, stepUpToken), ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) return null;
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<GenerateRecoveryCodesResultDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS recovery generate failed");
            return null;
        }
    }

    public async Task<IReadOnlyList<UserDeviceSummaryDto>?> GetDevicesAsync(string customerToken, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("GET", "devices", customerToken), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<IReadOnlyList<UserDeviceSummaryDto>>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS devices failed");
            return null;
        }
    }

    public async Task<UserDeviceDetailDto?> GetDeviceAsync(string customerToken, Guid deviceId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("GET", $"devices/{deviceId}", customerToken), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<UserDeviceDetailDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS device detail failed");
            return null;
        }
    }

    public async Task<bool> RenameDeviceAsync(string customerToken, Guid deviceId, string friendlyName, CancellationToken ct = default)
    {
        try
        {
            var request = Create("PATCH", $"devices/{deviceId}/rename", customerToken);
            request.Content = JsonContent.Create(new { friendlyName });
            var resp = await _http.SendAsync(request, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS device rename failed");
            return false;
        }
    }

    public async Task<bool> RevokeDeviceAsync(string customerToken, string stepUpToken, Guid deviceId, CancellationToken ct = default)
    {
        try
        {
            var request = Create("POST", $"devices/{deviceId}/revoke", customerToken, stepUpToken);
            request.Content = JsonContent.Create(new { reasonCode = "USER_REQUESTED" });
            var resp = await _http.SendAsync(request, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS device revoke failed");
            return false;
        }
    }

    public async Task<IReadOnlyList<DeviceCredentialSummaryDto>?> GetCredentialsAsync(string customerToken, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("GET", "webauthn/credentials", customerToken), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<IReadOnlyList<DeviceCredentialSummaryDto>>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS credentials failed");
            return null;
        }
    }

    public async Task<bool> RevokeCredentialAsync(string customerToken, string stepUpToken, Guid credentialId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("POST", $"webauthn/credentials/{credentialId}/revoke", customerToken, stepUpToken), ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS credential revoke failed");
            return false;
        }
    }

    public async Task<BeginRegistrationResultDto?> BeginRegistrationAsync(string customerToken, string origin, string rpId, Guid? deviceRecordId, CancellationToken ct = default)
    {
        try
        {
            var request = Create("POST", "webauthn/registration/begin", customerToken);
            request.Content = JsonContent.Create(new { origin, rpId, deviceRecordId });
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<BeginRegistrationResultDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS webauthn begin failed");
            return null;
        }
    }

    public async Task<FinishRegistrationResultDto?> FinishRegistrationAsync(string customerToken, string challenge, string origin, string rpId, JsonElement attestationResponse, Guid? deviceRecordId, CancellationToken ct = default)
    {
        try
        {
            var request = Create("POST", "webauthn/registration/finish", customerToken);
            request.Content = JsonContent.Create(new { challenge, origin, rpId, attestationResponse, deviceRecordId });
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<FinishRegistrationResultDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS webauthn finish failed");
            return null;
        }
    }

    public async Task<BeginAuthenticationResultDto?> BeginAuthenticationAsync(string customerToken, string origin, string rpId, CancellationToken ct = default)
    {
        try
        {
            var request = Create("POST", "webauthn/authentication/begin", customerToken);
            request.Content = JsonContent.Create(new { origin, rpId });
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<BeginAuthenticationResultDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS webauthn auth begin failed");
            return null;
        }
    }

    public async Task<FinishAuthenticationResultDto?> FinishAuthenticationAsync(string customerToken, string challenge, string origin, string rpId, JsonElement assertionResponse, CancellationToken ct = default)
    {
        try
        {
            var request = Create("POST", "webauthn/authentication/finish", customerToken);
            request.Content = JsonContent.Create(new { challenge, origin, rpId, assertionResponse });
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<FinishAuthenticationResultDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS webauthn auth finish failed");
            return null;
        }
    }

    // ---- DTOs (mirror VanAn.Atrs.Contracts) ----
    public sealed record TrustProfileSummaryDto(
        Guid TrustSubjectId, string Status, string AssuranceState,
        int ActiveDeviceCount, int ActivePasskeyCount, bool HasRecoveryCodes, DateTime? LastSecurityActivityAt);

    public sealed record RecoveryCodesStatusDto(bool HasActiveSet, int RemainingCount, DateTime? GeneratedAt, DateTime? LastRegeneratedAt);

    public sealed record GenerateRecoveryCodesResultDto(Guid SetId, IReadOnlyList<string> Codes, DateTime GeneratedAt);

    public sealed record DeviceCredentialSummaryDto(
        Guid Id, string CredentialId, string RpId, string[]? Transports,
        bool? BackupEligible, bool? BackupState, string Status, DateTime CreatedAt, DateTime? LastUsedAt);

    public sealed record UserDeviceSummaryDto(
        Guid Id, string DeviceId, string DeviceType, string? Platform, string? FriendlyName,
        string RegistrationMethod, string Status, DateTime FirstSeenAt, DateTime LastSeenAt,
        DateTime? RevokedAt, int ActiveCredentialCount);

    public sealed record UserDeviceDetailDto(UserDeviceSummaryDto Device, IReadOnlyList<DeviceCredentialSummaryDto> Credentials);

    public sealed record BeginRegistrationResultDto(string Challenge, JsonElement Options, DateTime ExpiresAt);
    public sealed record BeginAuthenticationResultDto(string Challenge, JsonElement Options, DateTime ExpiresAt);
    public sealed record FinishRegistrationResultDto(bool Success, string Reason, DeviceCredentialSummaryDto? Credential, Guid? DeviceRecordId);
    public sealed record FinishAuthenticationResultDto(bool Success, string Reason, Guid? CredentialId, string? StepUpToken);
}

internal static class HttpRequestExtensions
{
    public static HttpRequestMessage WithJson(this HttpRequestMessage request, object body)
    {
        request.Content = JsonContent.Create(body);
        return request;
    }
}

/// <summary>WebAuthn JS interop — navigator.credentials.create/get (webauthn.js).</summary>
public static class WebAuthnJs
{
    /// <summary>navigator.credentials.create — trả attestation response JSON (hoặc null khi user cancel/timeout/unsupported).</summary>
    public static Task<JsonElement?> CreateCredentialAsync(IJSRuntime js, JsonElement options)
        => js.InvokeAsync<JsonElement?>("vananWebAuthn.createCredential", options).AsTask();

    /// <summary>navigator.credentials.get — trả assertion response JSON (hoặc null khi cancel/unsupported).</summary>
    public static Task<JsonElement?> GetAssertionAsync(IJSRuntime js, JsonElement options)
        => js.InvokeAsync<JsonElement?>("vananWebAuthn.getAssertion", options).AsTask();
}