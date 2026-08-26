using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// Crawl-to-Onboard Phase 7 (2026-08-26): ShopERP client for Gateway crawl-onboarding admin endpoints.
/// Wraps: Pending tenants, Direct verify, Duplicates, Claims queue (list/approve/reject), Crawl trigger.
/// All endpoints are SystemAdmin-only (Gateway mints SystemAdmin JWT via base class).
/// Follows pattern of TenantApiClient.cs — inherits GatewayAdminApiClientBase.
/// </summary>
public sealed class TenantClaimApiClient : GatewayAdminApiClientBase
{
    public TenantClaimApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        AuthenticationStateProvider authStateProvider,
        ILogger<TenantClaimApiClient> logger)
        : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

    // ── Pending tenants ───────────────────────────────────────────────────

    /// <summary>GET /api/v1/tenants/pending — list all Pending tenants (crawled, not verified).</summary>
    public async Task<List<PendingTenantApiDto>> ListPendingAsync(CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Get, "api/v1/tenants/pending");
        return await SendAndReadAsync<List<PendingTenantApiDto>>(HttpClient, req, ct) ?? new();
    }

    /// <summary>POST /api/v1/tenants/{id}/verify — direct verify (bypass claim flow).</summary>
    public async Task<VerifyResultApiDto> DirectVerifyAsync(Guid tenantId, VerifyTenantApiRequest request, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, $"api/v1/tenants/{tenantId}/verify", request);
        return await SendAndReadAsync<VerifyResultApiDto>(HttpClient, req, ct)
            ?? throw new InvalidOperationException("Verify returned empty response.");
    }

    // ── Duplicates ────────────────────────────────────────────────────────

    /// <summary>GET /api/v1/tenants/duplicates — list tenants with PotentialDuplicateOf != null.</summary>
    public async Task<List<DuplicateTenantApiDto>> ListDuplicatesAsync(CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Get, "api/v1/tenants/duplicates");
        return await SendAndReadAsync<List<DuplicateTenantApiDto>>(HttpClient, req, ct) ?? new();
    }

    /// <summary>POST /api/v1/tenants/duplicates/resolve — keep one, deactivate other.</summary>
    public async Task ResolveDuplicateAsync(Guid keepTenantId, Guid deactivateTenantId, string reason, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, "api/v1/tenants/duplicates/resolve",
            new { KeepTenantId = keepTenantId, DeactivateTenantId = deactivateTenantId, Reason = reason });
        var resp = await HttpClient.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    // ── Claims queue ──────────────────────────────────────────────────────

    /// <summary>GET /api/v1/claims — list all Submitted claims (SysAdmin queue).</summary>
    public async Task<List<ClaimApiDto>> ListClaimsAsync(CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Get, "api/v1/claims");
        return await SendAndReadAsync<List<ClaimApiDto>>(HttpClient, req, ct) ?? new();
    }

    /// <summary>GET /api/v1/claims/{id} — single claim detail.</summary>
    public async Task<ClaimApiDto?> GetClaimAsync(Guid claimId, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Get, $"api/v1/claims/{claimId}");
        var resp = await HttpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ClaimApiDto>(GatewayJsonOptions, ct);
    }

    /// <summary>POST /api/v1/claims/{id}/approve — approve claim → VerifyAsync → returns credentials ONCE.</summary>
    public async Task<ClaimApprovalApiResult> ApproveClaimAsync(Guid claimId, VerifyTenantApiRequest verifyConfig, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, $"api/v1/claims/{claimId}/approve",
            new { VerifyConfig = verifyConfig });
        return await SendAndReadAsync<ClaimApprovalApiResult>(HttpClient, req, ct)
            ?? throw new InvalidOperationException("Approve returned empty response.");
    }

    /// <summary>POST /api/v1/claims/{id}/reject — reject claim with reason.</summary>
    public async Task RejectClaimAsync(Guid claimId, string reason, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, $"api/v1/claims/{claimId}/reject",
            new { Reason = reason });
        var resp = await HttpClient.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    // ── Crawl trigger ─────────────────────────────────────────────────────

    /// <summary>POST /api/v1/crawl/trigger — forward to crawler worker (port 5010). Returns 202 Accepted.</summary>
    public async Task<CrawlTriggerApiResult> TriggerCrawlAsync(CrawlTriggerApiRequest request, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, "api/v1/crawl/trigger", request);
        var resp = await HttpClient.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CrawlTriggerApiResult>(GatewayJsonOptions, ct)
            ?? new CrawlTriggerApiResult("Crawl trigger forwarded.", request.Source, request.Industry, request.Province, request.MaxResults);
    }
}

// ── Local DTOs (camelCase JSON matching Gateway responses) ──────────────────

public record PendingTenantApiDto(
    Guid Id,
    string Name,
    string? TaxCode,
    string? Address,
    Guid? PotentialDuplicateOf,
    DateTime CreatedAt);

public record VerifyTenantApiRequest(
    string OwnerUsername,
    string OwnerPassword,
    string OwnerDisplayName,
    string? OwnerPhone = null,
    string? OwnerEmail = null,
    Guid? ShopInstanceId = null,
    string? Slug = null);

public record VerifyResultApiDto(
    Guid TenantId,
    Guid OwnerUserId,
    int PermissionGroupsCreated,
    string PublishedSlug);

public record DuplicateTenantApiDto(
    Guid TenantId,
    string TenantName,
    string? TaxCode,
    Guid? PotentialDuplicateOf,
    string? CanonicalTenantName,
    string Status,
    DateTime CreatedAt);

public record ClaimApiDto(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string ClaimantName,
    string ClaimantPhone,
    string? ClaimantEmail,
    string GpkdImageUrl,
    string TaxCodeSubmitted,
    string Status,
    DateTime SubmittedAt,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    string? RejectionReason);

public record ClaimApprovalApiResult(
    Guid TenantId,
    Guid OwnerUserId,
    int PermissionGroupsCreated,
    string PublishedSlug,
    string OwnerUsername,
    string OwnerPassword,
    string Warning);

public record CrawlTriggerApiRequest(
    string? Source = null,
    string? Industry = null,
    string? Province = null,
    int MaxResults = 100);

public record CrawlTriggerApiResult(
    string Message,
    string? Source,
    string? Industry,
    string? Province,
    int MaxResults);
