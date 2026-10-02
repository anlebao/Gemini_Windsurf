using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// Membership Infrastructure (2026-10-02, user directive): ShopERP client cho 2 luồng admin
/// (SystemAdmin JWT) — Luồng 1 tenant→HTX · Luồng 2 member add + nâng cấp CTV.
/// Owner dùng MembershipApiClient (HtxMembershipOfficer — HTX self-config) — client này KHÔNG cho Owner.
/// </summary>
public sealed class MembershipAdminApiClient : GatewayAdminApiClientBase
{
    public MembershipAdminApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        AuthenticationStateProvider authStateProvider,
        ILogger<MembershipAdminApiClient> logger)
        : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

    protected override string GatewayRole => "SystemAdmin";

    /// <summary>GET /api/membership/htx-profiles — danh sách HTX đã kích hoạt (bộ chọn review).</summary>
    public async Task<List<HtxProfileSummaryApiDto>> ListHtxProfilesAsync(CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Get, "api/membership/htx-profiles");
        return await SendAndReadAsync<List<HtxProfileSummaryApiDto>>(HttpClient, req, ct) ?? new();
    }

    /// <summary>POST /api/admin/membership/htx-profile — Luồng 1: tenant → HTX (tài liệu tùy chọn qua charterUrl).</summary>
    public async Task CreateHtxProfileForTenantAsync(
        Guid htxTenantId, string? charterVersion = null, string? charterUrl = null, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, "api/admin/membership/htx-profile",
            new { HtxTenantId = htxTenantId, CharterVersion = charterVersion, CharterUrl = charterUrl });
        var resp = await HttpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            string body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"CreateHtxProfile failed ({(int)resp.StatusCode}): {body}");
        }
    }

    /// <summary>POST /api/admin/membership/members — Luồng 2: thêm tenant làm thành viên HTX.</summary>
    public async Task<AddMemberResultApiDto> AddMemberAsync(
        Guid htxTenantId, Guid memberTenantId, string membershipType, decimal? capitalContributionAmount = null,
        CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, "api/admin/membership/members",
            new { HtxTenantId = htxTenantId, MemberTenantId = memberTenantId, MembershipType = membershipType, CapitalContributionAmount = capitalContributionAmount });
        return await SendAndReadAsync<AddMemberResultApiDto>(HttpClient, req, ct)
            ?? throw new InvalidOperationException("AddMember returned empty response.");
    }

    /// <summary>POST /api/admin/membership/collaborator-upgrade — D4: nâng cấp Salesman/Shipper thành tenant (idempotent).</summary>
    public async Task<UpgradeResultApiDto> UpgradeCollaboratorAsync(
        Guid customerId, string? displayName = null, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, "api/admin/membership/collaborator-upgrade",
            new { CustomerId = customerId, DisplayName = displayName });
        return await SendAndReadAsync<UpgradeResultApiDto>(HttpClient, req, ct)
            ?? throw new InvalidOperationException("UpgradeCollaborator returned empty response.");
    }

    /// <summary>GET /api/admin/membership/members?htxTenantId= — danh sách thành viên của HTX (SystemAdmin).</summary>
    public async Task<List<MemberApiDto>> ListMembersAsync(Guid htxTenantId, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Get, $"api/admin/membership/members?htxTenantId={htxTenantId}");
        return await SendAndReadAsync<List<MemberApiDto>>(HttpClient, req, ct) ?? new();
    }

    /// <summary>
    /// POST /api/v1/images/upload — upload tài liệu (Luồng 1, tùy chọn) → Cloudinary URL (AllowAnonymous, rate-limited).
    /// </summary>
    public async Task<string> UploadDocumentAsync(byte[] bytes, string fileName, string folder = "htx-documents", CancellationToken ct = default)
    {
        if (bytes is null || bytes.Length == 0)
            throw new ArgumentException("No file content.", nameof(bytes));

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        });
        form.Add(fileContent, "file", fileName);

        var response = await HttpClient.PostAsync($"api/v1/images/upload?folder={folder}", form, ct);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Upload failed ({(int)response.StatusCode}): {body}");
        }

        using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (doc.RootElement.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == System.Text.Json.JsonValueKind.String)
            return urlEl.GetString()!;
        if (doc.RootElement.TryGetProperty("Url", out var urlEl2) && urlEl2.ValueKind == System.Text.Json.JsonValueKind.String)
            return urlEl2.GetString()!;
        throw new InvalidOperationException("Upload response missing 'url'.");
    }
}

public sealed record HtxProfileSummaryApiDto(Guid HtxTenantId, string TenantName, string CharterVersion);
public sealed record AddMemberResultApiDto(Guid MemberId, string? MemberNumber);
public sealed record UpgradeResultApiDto(Guid TenantId, bool Created);
