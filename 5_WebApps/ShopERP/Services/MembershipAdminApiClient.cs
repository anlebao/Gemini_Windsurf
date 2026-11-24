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

    /// <summary>POST /api/admin/membership/htx-profile/revert — 2026-10-15: HTX → Công ty/HKD
    /// (chặn khi còn thành viên). TargetType: HKD | Enterprise_SME | Enterprise_Large.</summary>
    public async Task RevertHtxProfileAsync(
        Guid htxTenantId, string targetType, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, "api/admin/membership/htx-profile/revert",
            new { HtxTenantId = htxTenantId, TargetType = targetType });
        var resp = await HttpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            string body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"RevertHtxProfile failed ({(int)resp.StatusCode}): {body}");
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
        var (_, url) = await UploadImageCoreAsync(bytes, fileName, folder, kind: null, ct);
        return url!;
    }

    /// <summary>
    /// Storefront 2-ảnh (2026-11-24): POST /api/v1/images/upload?folder=onboarding&amp;kind=storefront
    /// → trả (R2 key, presigned URL). Client PHẢI lưu KEY (presigned URL TTL 7 ngày sẽ hết hạn —
    /// store-info resolve presigned mới mỗi lần đọc).
    /// </summary>
    public async Task<(string? Key, string? Url)> UploadStorefrontImageAsync(byte[] bytes, string fileName, CancellationToken ct = default)
        => await UploadImageCoreAsync(bytes, fileName, "onboarding", "storefront", ct);

    private async Task<(string? Key, string? Url)> UploadImageCoreAsync(byte[] bytes, string fileName, string folder, string? kind, CancellationToken ct)
    {
        if (bytes is null || bytes.Length == 0)
            throw new ArgumentException("No file content.", nameof(bytes));

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".jpeg" or ".jpg" => "image/jpeg",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        });
        form.Add(fileContent, "file", fileName);

        var query = $"api/v1/images/upload?folder={folder}" + (kind is not null ? $"&kind={kind}" : string.Empty);
        var response = await HttpClient.PostAsync(query, form, ct);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Upload failed ({(int)response.StatusCode}): {body}");
        }

        using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        string? GetStr(string name) =>
            doc.RootElement.TryGetProperty(name, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String
                ? el.GetString() : null;
        var url = GetStr("url") ?? GetStr("Url");
        var key = GetStr("key") ?? GetStr("Key");
        if (url is null) throw new InvalidOperationException("Upload response missing 'url'.");
        return (key, url);
    }
}

public sealed record HtxProfileSummaryApiDto(Guid HtxTenantId, string TenantName, string CharterVersion);
public sealed record AddMemberResultApiDto(Guid MemberId, string? MemberNumber);
public sealed record UpgradeResultApiDto(Guid TenantId, bool Created);
