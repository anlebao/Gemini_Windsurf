using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// ATRS-9/ATRS-10 (Wave 3): ShopERP staff client → vanan-atrs admin API qua Gateway YARP
/// (/api/v1/account-trust/admin/* → atrs:5080). JWT mint GIỮ role thật của user
/// (SystemAdmin → break-glass; role khác → ATRS check StaffPermissionGrant theo sub=DemoUserId)
/// — KHÔNG mint SystemAdmin cho mọi caller, nếu không RBAC bị bypass.
/// Business logic ở atrs; trang Razor chỉ render + gọi service này.
/// </summary>
public sealed class AtrsAdminApiClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IJwtTokenService jwtTokenService,
    AuthenticationStateProvider authStateProvider,
    ILogger<AtrsAdminApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    // ── DTOs (mirror VanAn.Atrs.Contracts — JSON camelCase) ──

    public sealed record DisputeCaseSummary(
        Guid Id, string CaseType, string Status, Guid? TenantId, string ClaimedIssue,
        DateTime SubmittedAt, DateTime? ClosedAt, string? UserFacingStatusNote);

    public sealed record DisputeQueueItem(
        Guid Id, string CaseType, string Status, Guid? TenantId, Guid TrustSubjectId,
        Guid? AssignedReviewerId, DateTime SubmittedAt, int EvidenceCount, int PendingDecisionCount);

    public sealed record DisputeEvidenceItem(
        Guid Id, string EvidenceType, string? SourceReference, string? Description,
        DateTime RecordedAt, string? AccessClassification, string? ReliabilityClass, string? SubmittedBy);

    public sealed record DisputeDecision(
        Guid Id, string Decision, string Status, string DecisionReason,
        string? UserFacingExplanation, string? InternalRationale,
        int? ProposedAdjustmentPoints, Guid? ProposedAdjustmentTenantId, string? ProposedAdjustmentReason,
        Guid ProposedBy, Guid? ApprovedBy, DateTime ProposedAt, DateTime? ExecutedAt,
        string? ResolutionReference, string? RejectionReason);

    public sealed record LedgerEntry(
        string Id, string Type, int Points, int? BalanceAfter, string Reason,
        string? Reference, DateTime OccurredAt, Guid TenantId, string Source, bool IsReversed);

    public sealed record DisputeStaffDetail(
        Guid Id, string CaseType, string Status, Guid? TenantId, Guid TrustSubjectId,
        string ClaimedIssue, IReadOnlyList<string> RelatedTransactionIds,
        IReadOnlyList<string> RelatedLedgerReferences, Guid? AssignedReviewerId,
        DateTime SubmittedAt, DateTime? ClosedAt, string? UserFacingStatusNote,
        IReadOnlyList<DisputeEvidenceItem> Evidence,
        IReadOnlyList<DisputeDecision> Decisions,
        IReadOnlyList<LedgerEntry> Transactions);

    public sealed record ProposeDecisionBody(
        string Decision, string DecisionReason, string? UserFacingExplanation,
        string? InternalRationale, string PolicyVersion,
        int? ProposedAdjustmentPoints, Guid? ProposedAdjustmentTenantId, string? ProposedAdjustmentReason);

    // ── API ──

    public async Task<IReadOnlyList<DisputeQueueItem>> GetQueueAsync(string? status, int skip, int take, CancellationToken ct = default)
    {
        var url = $"api/v1/account-trust/admin/disputes?skip={skip}&take={take}";
        if (!string.IsNullOrWhiteSpace(status)) url += $"&status={Uri.EscapeDataString(status)}";
        return await SendAsync<IReadOnlyList<DisputeQueueItem>>(HttpMethod.Get, url, null, ct) ?? [];
    }

    public Task<DisputeStaffDetail?> GetDetailAsync(Guid caseId, CancellationToken ct = default)
        => SendAsync<DisputeStaffDetail>(HttpMethod.Get, $"api/v1/account-trust/admin/disputes/{caseId}", null, ct);

    public Task<DisputeQueueItem?> AssignAsync(Guid caseId, Guid staffUserId, CancellationToken ct = default)
        => SendAsync<DisputeQueueItem>(HttpMethod.Post, $"api/v1/account-trust/admin/disputes/{caseId}/assign",
            new { staffUserId }, ct);

    public Task<DisputeCaseSummary?> TransitionAsync(Guid caseId, string status, string? note, CancellationToken ct = default)
        => SendAsync<DisputeCaseSummary>(HttpMethod.Post, $"api/v1/account-trust/admin/disputes/{caseId}/transition",
            new { status, note }, ct);

    public Task<DisputeDecision?> ProposeDecisionAsync(Guid caseId, ProposeDecisionBody body, CancellationToken ct = default)
        => SendAsync<DisputeDecision>(HttpMethod.Post, $"api/v1/account-trust/admin/disputes/{caseId}/decisions", body, ct);

    public Task<DisputeDecision?> ApproveDecisionAsync(Guid decisionId, CancellationToken ct = default)
        => SendAsync<DisputeDecision>(HttpMethod.Post, $"api/v1/account-trust/admin/disputes/decisions/{decisionId}/approve", null, ct);

    public Task<DisputeDecision?> RejectDecisionAsync(Guid decisionId, string rejectionReason, CancellationToken ct = default)
        => SendAsync<DisputeDecision>(HttpMethod.Post, $"api/v1/account-trust/admin/disputes/decisions/{decisionId}/reject",
            new { rejectionReason }, ct);

    /// <summary>Staff user id hiện tại (sub claim) — cho assign-to-me.</summary>
    public async Task<Guid> CurrentUserIdAsync()
    {
        var auth = await authStateProvider.GetAuthenticationStateAsync();
        var sub = auth.User.FindFirst("sub")?.Value ?? auth.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    // ── Internals ──

    private async Task<T?> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("GatewayClient");
        client.BaseAddress = new Uri(configuration["Gateway:BaseUrl"] ?? "http://localhost:5001");

        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, await MintTokenAsync());
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);

        var resp = await client.SendAsync(request, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return default;
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync(ct);
            logger.LogWarning("AtrsAdminApi {Method} {Url} → {Status}: {Body}", method, url, resp.StatusCode, errBody);
            throw new InvalidOperationException($"ATRS API {(int)resp.StatusCode}: {errBody}");
        }
        return await resp.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }

    /// <summary>
    /// Mint JWT giữ role thật: SystemAdmin → "SystemAdmin" (break-glass);
    /// còn lại → role claim đầu tiên của user (ATRS tra grant theo sub).
    /// </summary>
    private async Task<string> MintTokenAsync()
    {
        var auth = await authStateProvider.GetAuthenticationStateAsync();
        var user = auth.User;

        var userIdStr = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.TryParse(userIdStr, out var id) ? id : Guid.NewGuid();
        var email = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value ?? "staff@vanan.vn";

        string tenantIdStr = user.FindFirst("tenant_id")?.Value ?? user.FindFirst("TenantId")?.Value ?? "";
        var tenantId = Guid.TryParse(tenantIdStr, out var tid) ? tid : Guid.Empty;

        // Giữ role thật — SystemAdmin break-glass; role khác (Owner/Staff/...) → ATRS check grant.
        string role = user.IsInRole("SystemAdmin")
            ? "SystemAdmin"
            : user.FindAll(ClaimTypes.Role).Select(c => c.Value).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r)) ?? "Staff";

        return jwtTokenService.GenerateToken(userId, email, role, tenantId);
    }
}
