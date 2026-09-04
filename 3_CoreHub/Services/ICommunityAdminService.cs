using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S6 (Sprint 6): Community admin service — eligible customer list, activate/deactivate roles.
/// Used by CommunityAdminController (SystemAdmin JWT auth).
/// </summary>
public interface ICommunityAdminService
{
    /// <summary>
    /// Get customers eligible for community role activation.
    /// Criteria: IdentityLevel >= Verified OR IdentityLevel >= DeviceVerified (v1.2) AND LoyaltyPoints >= 1000.
    /// Left join CommunityRoles to show existing roles. Paginated.
    /// </summary>
    Task<PagedResult<EligibleCustomerDto>> GetEligibleCustomersAsync(int page, int pageSize);

    /// <summary>
    /// Activate a community role for a customer. Verifies eligibility + no duplicate active role.
    /// </summary>
    Task<CommunityRole> ActivateRoleAsync(Guid customerId, CommunityRoleType role, Guid activatedBy);

    /// <summary>
    /// Deactivate an active community role for a customer.
    /// </summary>
    Task DeactivateRoleAsync(Guid customerId, CommunityRoleType role);

    /// <summary>
    /// Get all community roles for a customer (active + inactive).
    /// </summary>
    Task<List<CommunityRole>> GetCustomerRolesAsync(Guid customerId);

    // === R2 (2026-09-04): Tenant-scoped overloads — for Owner (Reseller owner) role management ===

    /// <summary>
    /// R2: Get eligible customers LIMITED to a specific tenant (Owner scope).
    /// Same eligibility criteria (IdentityLevel + LoyaltyPoints) but filtered by tenantId.
    /// Used by TenantCommunityAdminController (Owner endpoints) — IDOR safe.
    /// </summary>
    Task<PagedResult<EligibleCustomerDto>> GetEligibleCustomersForTenantAsync(Guid tenantId, int page, int pageSize);

    /// <summary>
    /// R2: Activate a community role for a customer — verify customer belongs to tenantId (IDOR guard).
    /// Throws UnauthorizedAccessException if customer.TenantId != tenantId.
    /// </summary>
    Task<CommunityRole> ActivateRoleForTenantAsync(Guid tenantId, Guid customerId, CommunityRoleType role, Guid activatedBy);

    /// <summary>
    /// R2: Deactivate an active community role — verify customer belongs to tenantId (IDOR guard).
    /// Throws UnauthorizedAccessException if role.TenantId != tenantId.
    /// </summary>
    Task DeactivateRoleForTenantAsync(Guid tenantId, Guid customerId, CommunityRoleType role);

    /// <summary>
    /// R2: Get all community roles for a customer — verify customer belongs to tenantId (IDOR guard).
    /// Throws UnauthorizedAccessException if customer.TenantId != tenantId.
    /// </summary>
    Task<List<CommunityRole>> GetCustomerRolesForTenantAsync(Guid tenantId, Guid customerId);
}

/// <summary>Paged result wrapper.</summary>
public class PagedResult<T>
{
    public int Total { get; set; }
    public List<T> Items { get; set; } = new();
}

/// <summary>DTO for eligible customer list.</summary>
public class EligibleCustomerDto
{
    public Guid CustomerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int LoyaltyPoints { get; set; }
    public string IdentityLevel { get; set; } = string.Empty;
    public List<string> ExistingRoles { get; set; } = new();
}
