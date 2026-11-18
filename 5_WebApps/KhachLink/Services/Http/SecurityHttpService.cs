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

    // ═══ ATRS-21: discoverable passkey login (PUBLIC — không cần customer token) ═══

    /// <summary>Begin passkey login — AssertionOptions discoverable (allowCredentials rỗng).</summary>
    public async Task<BeginPasskeyLoginResultDto?> BeginPasskeyLoginAsync(string origin, string rpId, CancellationToken ct = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/account-trust/passkey/login/begin");
            request.Content = JsonContent.Create(new { origin, rpId });
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<BeginPasskeyLoginResultDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS passkey login begin failed");
            return null;
        }
    }

    /// <summary>Finish passkey login — assertion → customer_token (hoặc null khi fail).</summary>
    public async Task<FinishPasskeyLoginResultDto?> FinishPasskeyLoginAsync(
        string challenge, string origin, string rpId, JsonElement assertionResponse, string? deviceId, CancellationToken ct = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/account-trust/passkey/login/finish");
            request.Content = JsonContent.Create(new { challenge, origin, rpId, assertionResponse, deviceId });
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<FinishPasskeyLoginResultDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS passkey login finish failed");
            return null;
        }
    }

    /// <summary>
    /// ATRS-21: guest passkey registration — KHÔNG customer token, auth bằng
    /// X-Customer-Device-Id (atrs guest fallback ATRS-16 → guest TrustSubject).
    /// </summary>
    public async Task<BeginRegistrationResultDto?> BeginGuestPasskeyRegistrationAsync(string deviceId, string origin, string rpId, CancellationToken ct = default)
    {
        try
        {
            var request = GuestRequest("POST", "webauthn/registration/begin", deviceId);
            request.Content = JsonContent.Create(new { origin, rpId, deviceRecordId = (Guid?)null });
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<BeginRegistrationResultDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS guest passkey begin failed");
            return null;
        }
    }

    public async Task<FinishRegistrationResultDto?> FinishGuestPasskeyRegistrationAsync(
        string deviceId, string challenge, string origin, string rpId, JsonElement attestationResponse, CancellationToken ct = default)
    {
        try
        {
            var request = GuestRequest("POST", "webauthn/registration/finish", deviceId);
            request.Content = JsonContent.Create(new { challenge, origin, rpId, attestationResponse, deviceRecordId = (Guid?)null });
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<FinishRegistrationResultDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS guest passkey finish failed");
            return null;
        }
    }

    private HttpRequestMessage GuestRequest(string method, string path, string deviceId)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), $"api/v1/account-trust/{path}");
        request.Headers.Add("X-Customer-Device-Id", deviceId);
        return request;
    }

    // ═══ Wave 4 (ATRS-6 subset + U09-U13) ═══

    // ---- Personal evidence + consent (U02) ----
    public async Task<IReadOnlyList<PersonalEvidenceItemDto>?> GetPersonalEvidenceAsync(string customerToken, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("GET", "personal-evidence", customerToken), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<IReadOnlyList<PersonalEvidenceItemDto>>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS personal-evidence failed");
            return null;
        }
    }

    public async Task<PersonalEvidenceItemDto?> UpsertPersonalEvidenceAsync(string customerToken, string fieldKey, string? value, string privacyNoticeVersion, CancellationToken ct = default)
    {
        try
        {
            var request = Create("PUT", "personal-evidence", customerToken);
            request.Content = JsonContent.Create(new { fieldKey, value, privacyNoticeVersion });
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<PersonalEvidenceItemDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS personal-evidence upsert failed");
            return null;
        }
    }

    public async Task<IReadOnlyList<ConsentDto>?> GetConsentsAsync(string customerToken, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("GET", "consents", customerToken), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<IReadOnlyList<ConsentDto>>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS consents failed");
            return null;
        }
    }

    public async Task<ConsentDto?> GrantConsentAsync(string customerToken, string purpose, string privacyNoticeVersion, CancellationToken ct = default)
    {
        try
        {
            var request = Create("POST", "consents", customerToken);
            request.Content = JsonContent.Create(new { purpose, privacyNoticeVersion });
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<ConsentDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS consent grant failed");
            return null;
        }
    }

    public async Task<bool> WithdrawConsentAsync(string customerToken, Guid consentId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("POST", $"consents/{consentId}/withdraw", customerToken), ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS consent withdraw failed");
            return false;
        }
    }

    // ---- Recovery requests (U09/U10) ----
    public async Task<SubmitRecoveryResultDto?> SubmitRecoveryRequestAsync(string customerToken, string requestType, string idempotencyKey, string? evidenceSummaryJson = null, CancellationToken ct = default)
    {
        try
        {
            var request = Create("POST", "recovery-requests", customerToken);
            request.Content = JsonContent.Create(new { requestType, idempotencyKey, newDeviceId = (Guid?)null, evidenceSummaryJson });
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<SubmitRecoveryResultDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS recovery submit failed");
            return null;
        }
    }

    public async Task<IReadOnlyList<RecoveryRequestSummaryDto>?> ListMyRecoveryRequestsAsync(string customerToken, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("GET", "recovery-requests/mine", customerToken), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<IReadOnlyList<RecoveryRequestSummaryDto>>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS recovery list failed");
            return null;
        }
    }

    public async Task<RecoveryRequestSummaryDto?> GetRecoveryRequestAsync(string customerToken, Guid id, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("GET", $"recovery-requests/{id}", customerToken), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<RecoveryRequestSummaryDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS recovery get failed");
            return null;
        }
    }

    public async Task<RecoveryRequestSummaryDto?> CancelRecoveryRequestAsync(string customerToken, Guid id, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("POST", $"recovery-requests/{id}/cancel", customerToken), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<RecoveryRequestSummaryDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS recovery cancel failed");
            return null;
        }
    }

    /// <summary>Tiered proof ①: verify recovery code trong recovery request.</summary>
    public async Task<VerifyRecoveryCodeResultDto?> VerifyRecoveryCodeAsync(string customerToken, Guid requestId, string code, CancellationToken ct = default)
    {
        try
        {
            var request = Create("POST", $"recovery-requests/{requestId}/verify-code", customerToken);
            request.Content = JsonContent.Create(new { code, deviceId = (Guid?)null, highAccountValue = false });
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode)
            {
                // 400 trả { success:false, reason } — parse nhẹ
                try
                {
                    var err = await resp.Content.ReadFromJsonAsync<VerifyRecoveryCodeResultDto>(_json, ct);
                    return err;
                }
                catch { return null; }
            }
            return await resp.Content.ReadFromJsonAsync<VerifyRecoveryCodeResultDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS recovery verify-code failed");
            return null;
        }
    }

    // ---- Disputes (U11-U13) ----
    public async Task<IReadOnlyList<DisputeCaseSummaryDto>?> ListDisputesAsync(string customerToken, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("GET", "disputes", customerToken), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<IReadOnlyList<DisputeCaseSummaryDto>>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS disputes list failed");
            return null;
        }
    }

    public async Task<DisputeCaseDetailDto?> GetDisputeAsync(string customerToken, Guid id, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("GET", $"disputes/{id}", customerToken), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<DisputeCaseDetailDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS dispute get failed");
            return null;
        }
    }

    public async Task<SubmitDisputeResultDto?> SubmitDisputeAsync(string customerToken, string caseType, string claimedIssue, Guid? tenantId, IReadOnlyList<string>? transactionIds, string idempotencyKey, CancellationToken ct = default)
    {
        try
        {
            var request = Create("POST", "disputes", customerToken);
            request.Content = JsonContent.Create(new { caseType, claimedIssue, tenantId, relatedTransactionIds = transactionIds, relatedLedgerReferences = (IReadOnlyList<string>?)null, idempotencyKey });
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<SubmitDisputeResultDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS dispute submit failed");
            return null;
        }
    }

    public async Task<DisputeCaseDetailDto?> AddDisputeEvidenceAsync(string customerToken, Guid caseId, string evidenceType, string? description, CancellationToken ct = default)
    {
        try
        {
            var request = Create("POST", $"disputes/{caseId}/evidence", customerToken);
            request.Content = JsonContent.Create(new { evidenceType, sourceSystem = "khachlink", sourceReference = (string?)null, integrityHash = (string?)null, description, capturedAt = (DateTime?)null, accessClassification = (string?)null });
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<DisputeCaseDetailDto>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS dispute evidence failed");
            return null;
        }
    }

    public async Task<bool> WithdrawDisputeAsync(string customerToken, Guid caseId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("POST", $"disputes/{caseId}/withdraw", customerToken), ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS dispute withdraw failed");
            return false;
        }
    }

    /// <summary>FR-013: transaction history của customer tại 1 tenant (cho dispute selector).</summary>
    public async Task<IReadOnlyList<LoyaltyLedgerEntryDto>?> GetTransactionsAsync(string customerToken, Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("GET", $"transactions?tenantId={tenantId}", customerToken), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<IReadOnlyList<LoyaltyLedgerEntryDto>>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS transactions failed");
            return null;
        }
    }

    // ---- Attachments (D9) ----
    /// <summary>Upload file base64 → R2. Trả attachment DTO (có signed URL) hoặc null.</summary>
    public async Task<EvidenceAttachmentDto?> UploadDisputeAttachmentAsync(string customerToken, Guid caseId, string fileName, string contentType, string dataBase64, CancellationToken ct = default)
    {
        try
        {
            var request = Create("POST", $"disputes/{caseId}/attachments", customerToken);
            request.Content = JsonContent.Create(new { fileName, contentType, dataBase64 });
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var result = await resp.Content.ReadFromJsonAsync<UploadAttachmentResultDto>(_json, ct);
            return result?.Attachment;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS attachment upload failed");
            return null;
        }
    }

    public async Task<IReadOnlyList<EvidenceAttachmentDto>?> ListDisputeAttachmentsAsync(string customerToken, Guid caseId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(Create("GET", $"disputes/{caseId}/attachments", customerToken), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<IReadOnlyList<EvidenceAttachmentDto>>(_json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "ATRS attachments list failed");
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

    // ATRS-21 DTOs
    public sealed record BeginPasskeyLoginResultDto(string Challenge, JsonElement Options, DateTime ExpiresAt);
    public sealed record FinishPasskeyLoginResultDto(bool Success, string? CustomerToken, Guid? CustomerId, bool CustomerCreated);

    // Wave 4 DTOs
    public sealed record PersonalEvidenceItemDto(
        string FieldKey, bool HasValue, string? MaskedValue, string Source,
        string VerificationStatus, string PrivacyNoticeVersion, DateTime? UpdatedAt);

    public sealed record ConsentDto(Guid Id, string Purpose, string PrivacyNoticeVersion, DateTime GrantedAt, DateTime? WithdrawnAt);

    public sealed record RecoveryRequestSummaryDto(
        Guid Id, string RequestType, string Status, Guid TrustSubjectId, Guid? NewDeviceId,
        string? RiskDecisionJson, string? ReviewerId, string? DecisionReasonCode,
        DateTime CreatedAt, DateTime? CompletedAt);

    public sealed record SubmitRecoveryResultDto(RecoveryRequestSummaryDto Request, bool Existing);
    public sealed record VerifyRecoveryCodeResultDto(bool Success, string Reason, RecoveryRequestSummaryDto? Request, string? RiskDecisionKind);

    public sealed record DisputeCaseSummaryDto(
        Guid Id, string CaseType, string Status, Guid? TenantId, string ClaimedIssue,
        DateTime SubmittedAt, DateTime? ClosedAt, string? UserFacingStatusNote);

    public sealed record DisputeEvidenceItemDto(
        Guid Id, string EvidenceType, string? SourceReference, string? Description,
        DateTime RecordedAt, string? AccessClassification = null,
        string? ReliabilityClass = null, string? SubmittedBy = null);

    public sealed record DisputeCaseDetailDto(
        Guid Id, string CaseType, string Status, Guid? TenantId, string ClaimedIssue,
        IReadOnlyList<string> RelatedTransactionIds, IReadOnlyList<string> RelatedLedgerReferences,
        DateTime SubmittedAt, DateTime? ClosedAt, string? UserFacingStatusNote,
        IReadOnlyList<DisputeEvidenceItemDto> Evidence, IReadOnlyList<string> UserVisibleTimeline);

    public sealed record SubmitDisputeResultDto(DisputeCaseSummaryDto Case, bool Existing);

    public sealed record LoyaltyLedgerEntryDto(
        string Id, string Type, int Points, int? BalanceAfter, string Reason,
        string? Reference, DateTime OccurredAt, Guid TenantId, string Source, bool IsReversed = false);

    public sealed record EvidenceAttachmentDto(
        Guid Id, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt, string? DownloadUrl);

    public sealed record UploadAttachmentResultDto(EvidenceAttachmentDto Attachment);
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