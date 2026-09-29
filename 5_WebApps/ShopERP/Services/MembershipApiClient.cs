using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// Membership Infrastructure (2026-09-29): ShopERP client cho Gateway membership endpoints (HTX review).
/// Mint JWT với role "Owner" (HtxMembershipOfficer — HTX quyết định kết nạp, KHÔNG phải SystemAdmin —
/// SRS §3.1, §6.5). tenant_id lấy từ claims của user đang đăng nhập (Owner của tenant HTX).
/// Các endpoint: applications queue (list/approve/reject/request-info), member registry (list/lifecycle).
/// </summary>
public sealed class MembershipApiClient : GatewayAdminApiClientBase
{
    public MembershipApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        AuthenticationStateProvider authStateProvider,
        ILogger<MembershipApiClient> logger)
        : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

    protected override string GatewayRole => "Owner";

    // ── Applications (SRS §15 dashboard) ────────────────────────────────────

    /// <summary>GET /api/membership/applications?status= — queue xét duyệt của HTX.</summary>
    public async Task<List<MembershipApplicationApiDto>> ListApplicationsAsync(string? status = null, CancellationToken ct = default)
    {
        var uri = status is null ? "api/membership/applications" : $"api/membership/applications?status={status}";
        var req = await CreateRequestAsync(HttpMethod.Get, uri);
        return await SendAndReadAsync<List<MembershipApplicationApiDto>>(HttpClient, req, ct) ?? new();
    }

    /// <summary>POST /api/membership/applications/{id}/approve — duyệt + kích hoạt Member.</summary>
    public async Task<Guid> ApproveAsync(Guid applicationId, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, $"api/membership/applications/{applicationId}/approve");
        var result = await SendAndReadAsync<ApproveResultApiDto>(HttpClient, req, ct)
            ?? throw new InvalidOperationException("Approve returned empty response.");
        return result.MemberId;
    }

    /// <summary>POST /api/membership/applications/{id}/reject — từ chối (kèm lý do).</summary>
    public async Task RejectAsync(Guid applicationId, string reason, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, $"api/membership/applications/{applicationId}/reject",
            new { Reason = reason });
        var resp = await HttpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            string body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Reject failed ({(int)resp.StatusCode}): {body}");
        }
    }

    /// <summary>POST /api/membership/applications/{id}/request-info — yêu cầu bổ sung.</summary>
    public async Task RequestMoreInfoAsync(Guid applicationId, string reason, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, $"api/membership/applications/{applicationId}/request-info",
            new { Reason = reason });
        var resp = await HttpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            string body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"RequestMoreInfo failed ({(int)resp.StatusCode}): {body}");
        }
    }

    // ── Member registry (SRS §17) ───────────────────────────────────────────

    /// <summary>GET /api/membership/members — sổ đăng ký thành viên của HTX.</summary>
    public async Task<List<MemberApiDto>> ListMembersAsync(string? status = null, CancellationToken ct = default)
    {
        var uri = status is null ? "api/membership/members" : $"api/membership/members?status={status}";
        var req = await CreateRequestAsync(HttpMethod.Get, uri);
        return await SendAndReadAsync<List<MemberApiDto>>(HttpClient, req, ct) ?? new();
    }

    /// <summary>POST /api/membership/members/{id}/suspend — tạm đình chỉ.</summary>
    public async Task SuspendMemberAsync(Guid memberId, string reason, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, $"api/membership/members/{memberId}/suspend",
            new { Reason = reason });
        var resp = await HttpClient.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>POST /api/membership/members/{id}/reactivate — khôi phục.</summary>
    public async Task ReactivateMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, $"api/membership/members/{memberId}/reactivate");
        var resp = await HttpClient.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }
}

// ── DTOs (mirror Gateway MembershipApplicationDto/MemberDto — camelCase + string enums) ──

public sealed record MembershipApplicationApiDto(
    Guid Id,
    Guid HtxTenantId,
    Guid ApplicantCustomerId,
    Guid? BusinessTenantId,
    string MembershipType,
    string Status,
    string FullName,
    string PhoneNumber,
    string? Email,
    string? Region,
    string ExpectedRole,
    string IdentityVerificationLevel,
    string ConsentVersion,
    string CharterVersion,
    string CapitalFeeStatus,
    DateTime? SubmittedAt,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    string? RejectionReason,
    string? NeedInfoReason,
    DateTime CreatedAt);

public sealed record MemberApiDto(
    Guid Id,
    Guid HtxTenantId,
    Guid? MemberCustomerId,
    Guid? MemberTenantId,
    string MembershipType,
    string MemberNumber,
    string Status,
    DateTime JoinedAt,
    DateTime? ApprovedAt,
    DateTime? EffectiveAt,
    DateTime? TerminatedAt,
    string? StatusReason);

public sealed record ApproveResultApiDto(Guid MemberId);
