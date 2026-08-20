using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// R2 Storage Admin API client — calls Gateway /api/r2storage endpoints.
/// Used by /admin/r2-storage Blazor page for per-tenant storage stats + manual cleanup.
/// </summary>
public sealed class R2StorageApiClient : GatewayAdminApiClientBase
{
    public R2StorageApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        AuthenticationStateProvider authStateProvider,
        ILogger<R2StorageApiClient> logger)
        : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger)
    {
    }

    /// <summary>Get storage stats for a tenant (photo count + total size).</summary>
    public async Task<TenantStorageStatsDto?> GetStatsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, $"api/r2storage/stats/{tenantId}");
        return await SendAndReadAsync<TenantStorageStatsDto>(HttpClient, request, ct);
    }

    /// <summary>Trigger immediate cleanup for a specific tenant.</summary>
    public async Task<R2CleanupResultDto?> TriggerCleanupAsync(Guid tenantId, int? retentionDays = null, CancellationToken ct = default)
    {
        var url = $"api/r2storage/cleanup/{tenantId}";
        if (retentionDays.HasValue)
            url += $"?retentionDays={retentionDays.Value}";
        var request = await CreateRequestAsync(HttpMethod.Post, url);
        return await SendAndReadAsync<R2CleanupResultDto>(HttpClient, request, ct);
    }

    /// <summary>Trigger immediate cleanup for ALL tenants (SystemAdmin only).</summary>
    public async Task<R2CleanupResultDto?> TriggerCleanupAllAsync(int? retentionDays = null, CancellationToken ct = default)
    {
        var url = "api/r2storage/cleanup-all";
        if (retentionDays.HasValue)
            url += $"?retentionDays={retentionDays.Value}";
        var request = await CreateRequestAsync(HttpMethod.Post, url);
        return await SendAndReadAsync<R2CleanupResultDto>(HttpClient, request, ct);
    }
}

/// <summary>DTO for tenant storage stats (matches Gateway TenantStorageStats record).</summary>
public record TenantStorageStatsDto(
    int PlatePhotoCount,
    int CustomerPhotoCount,
    long TotalSizeBytes,
    DateTime? OldestPhotoDate);

/// <summary>DTO for cleanup result (matches Gateway R2CleanupResult record).</summary>
public record R2CleanupResultDto(
    int SessionsProcessed,
    int PhotosDeleted,
    long BytesFreed,
    List<string> Errors);
