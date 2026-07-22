using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Tenant lifecycle management service — Wave 5.
    /// SystemAdmin-only operations: create, list, update profile, suspend, deactivate.
    /// </summary>
    public interface ITenantManagementService
    {
        /// <summary>Creates a new tenant and raises TenantCreatedEvent (welcome email).</summary>
        Task<Tenant> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default);

        /// <summary>Returns tenant by ID. Returns null if not found.</summary>
        Task<Tenant?> GetTenantByIdAsync(TenantId id, CancellationToken ct = default);

        /// <summary>Lists all tenants (no tenant filter — SystemAdmin sees all).</summary>
        Task<IReadOnlyList<Tenant>> ListTenantsAsync(CancellationToken ct = default);

        /// <summary>Updates tenant display name and settings.</summary>
        Task UpdateProfileAsync(TenantId id, UpdateTenantProfileRequest request, CancellationToken ct = default);

        /// <summary>Tenant Profile Page (2026-07-21): Update URL slug for /store/{slug} route.</summary>
        Task UpdateSlugAsync(TenantId id, string? slug, CancellationToken ct = default);

        /// <summary>Suspends an active tenant (reversible).</summary>
        Task SuspendAsync(TenantId id, string reason, CancellationToken ct = default);

        /// <summary>Reactivates a suspended tenant.</summary>
        Task ReactivateAsync(TenantId id, CancellationToken ct = default);

        /// <summary>Permanently deactivates a tenant (irreversible).</summary>
        Task DeactivateAsync(TenantId id, string reason, CancellationToken ct = default);

        /// <summary>Phase 6: Assign tenant to a ShopERP hosting instance (multi-VPS routing).</summary>
        Task AssignShopInstanceAsync(TenantId id, Guid shopInstanceId, CancellationToken ct = default);
    }

    public record CreateTenantRequest(
        string Name,
        BusinessType BusinessType,
        HKDGroup? HKDGroup,
        string? ContactEmail,
        string? ContactPhone,
        string? Address,
        string? TaxCode);

    public record UpdateTenantProfileRequest(
        string Name,
        string? ContactEmail,
        string? ContactPhone,
        string? Address,
        string? TaxCode,
        string? Slug = null,
        double? Latitude = null,
        double? Longitude = null,
        string? SocialLinksFb = null,
        string? SocialLinksTiktok = null,
        string? BrandStory = null,
        ThemeType Theme = ThemeType.Classic);
}
