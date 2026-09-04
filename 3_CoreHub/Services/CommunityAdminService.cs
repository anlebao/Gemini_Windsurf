using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S6 (Sprint 6): Community admin service implementation.
/// Eligible list, activate/deactivate roles. Cross-tenant (IgnoreQueryFilters).
/// R2.1 (2026-09-04): Per-tenant eligibility thresholds via IShopFeatureSettingsService
/// (replaces hard-coded 1000 points + IdentityLevel.Verified).
/// </summary>
public class CommunityAdminService(
    IVanAnDbContext dbContext,
    IShopFeatureSettingsService shopFeatureSettingsService,
    ILogger<CommunityAdminService> logger) : ICommunityAdminService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly IShopFeatureSettingsService _shopFeatureSettingsService = shopFeatureSettingsService;
    private readonly ILogger<CommunityAdminService> _logger = logger;

    /// <summary>
    /// R2.1: Get per-tenant eligibility thresholds for a specific role.
    /// Falls back to defaults (1000 points + Verified) if ShopFeatureSettings not configured.
    /// SystemAdmin cross-tenant path passes tenantId=null → uses defaults (backward compat).
    /// </summary>
    private async Task<(int MinPoints, IdentityLevel RequiredIdentityLevel)> GetEligibilityThresholdsAsync(
        Guid? tenantId, CommunityRoleType role)
    {
        // SystemAdmin cross-tenant path: no specific tenant → use hard-coded defaults (backward compat)
        if (tenantId == null || tenantId == Guid.Empty)
        {
            return (1000, IdentityLevel.Verified);
        }

        try
        {
            var settings = await _shopFeatureSettingsService.GetSettingsAsync(tenantId.Value);
            int minPoints = role == CommunityRoleType.Salesman
                ? settings.Community_SalesmanMinPoints
                : settings.Community_ShipperMinPoints;
            var requiredLevel = (IdentityLevel)settings.Community_RequiredIdentityLevel;
            return (minPoints, requiredLevel);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "GetEligibilityThresholdsAsync: Failed to load ShopFeatureSettings for tenant {TenantId}, using defaults",
                tenantId);
            return (1000, IdentityLevel.Verified);
        }
    }

    public async Task<PagedResult<EligibleCustomerDto>> GetEligibleCustomersAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        // Criteria: IdentityLevel >= Verified (2) OR DeviceVerified (4), AND LoyaltyPoints >= 1000
        // Note: DeviceVerified=4 > Verified=2, so >= Verified covers both.
        var query = _dbContext.Customers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.IsActive
                && c.IdentityLevel >= IdentityLevel.Verified
                && c.LoyaltyPoints >= 1000);

        var total = await query.CountAsync();

        var customers = await query
            .OrderByDescending(c => c.LoyaltyPoints)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.FullName,
                c.PhoneNumber,
                c.LoyaltyPoints,
                IdentityLevel = c.IdentityLevel.ToString()
            })
            .ToListAsync();

        // Load existing roles for these customers
        var customerIds = customers.Select(c => c.Id).ToList();
        var roles = await _dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => customerIds.Contains(r.CustomerId) && r.IsActive)
            .Select(r => new { r.CustomerId, RoleType = r.RoleType.ToString() })
            .ToListAsync();

        var rolesByCustomer = roles.GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.RoleType).ToList());

        var items = customers.Select(c => new EligibleCustomerDto
        {
            CustomerId = c.Id,
            FullName = c.FullName,
            PhoneNumber = MaskPhone(c.PhoneNumber),
            LoyaltyPoints = c.LoyaltyPoints,
            IdentityLevel = c.IdentityLevel,
            ExistingRoles = rolesByCustomer.TryGetValue(c.Id, out var existing) ? existing : new List<string>()
        }).ToList();

        return new PagedResult<EligibleCustomerDto> { Total = total, Items = items };
    }

    public async Task<CommunityRole> ActivateRoleAsync(Guid customerId, CommunityRoleType role, Guid activatedBy)
    {
        // 1. Verify customer exists + meets criteria
        var customer = await _dbContext.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer == null)
            throw new InvalidOperationException($"Customer {customerId} not found.");

        if (!customer.IsActive)
            throw new InvalidOperationException($"Customer {customerId} is not active.");

        if (customer.IdentityLevel < IdentityLevel.Verified || customer.LoyaltyPoints < 1000)
            throw new InvalidOperationException(
                $"Customer {customerId} does not meet eligibility criteria " +
                $"(IdentityLevel={customer.IdentityLevel}, LoyaltyPoints={customer.LoyaltyPoints}).");

        // 2. Check no active role of same type
        var existingRole = await _dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.CustomerId == customerId
                && r.RoleType == role
                && r.IsActive);

        if (existingRole != null)
            throw new InvalidOperationException(
                $"Customer {customerId} already has an active {role} role.");

        // 3. Create role
        var newRole = new CommunityRole(customer.TenantId, customerId, role, activatedBy);
        _dbContext.CommunityRoles.Add(newRole);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("ActivateRoleAsync: {Role} activated for customer {CustomerId} by {ActivatedBy}",
            role, customerId, activatedBy);

        return newRole;
    }

    public async Task DeactivateRoleAsync(Guid customerId, CommunityRoleType role)
    {
        var existingRole = await _dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.CustomerId == customerId
                && r.RoleType == role
                && r.IsActive);

        if (existingRole == null)
            throw new InvalidOperationException(
                $"No active {role} role found for customer {customerId}.");

        existingRole.Deactivate();
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("DeactivateRoleAsync: {Role} deactivated for customer {CustomerId}",
            role, customerId);
    }

    public async Task<List<CommunityRole>> GetCustomerRolesAsync(Guid customerId)
    {
        return await _dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.ActivatedAt)
            .ToListAsync();
    }

    // === R2 (2026-09-04): Tenant-scoped overloads — for Owner (Reseller owner) role management ===
    // IDOR guard: every method verifies customer/role belongs to the calling tenant before action.

    public async Task<PagedResult<EligibleCustomerDto>> GetEligibleCustomersForTenantAsync(Guid tenantId, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        // R2.1: Read per-tenant eligibility thresholds from ShopFeatureSettings.
        // For the eligible LIST, use the MORE PERMISSIVE of Salesman/Shipper thresholds
        // (so customer shows in list if eligible for at least one role).
        var salesmanThresholds = await GetEligibilityThresholdsAsync(tenantId, CommunityRoleType.Salesman);
        var shipperThresholds = await GetEligibilityThresholdsAsync(tenantId, CommunityRoleType.Shipper);
        int minPointsForList = Math.Min(salesmanThresholds.MinPoints, shipperThresholds.MinPoints);
        var requiredLevelForList = (IdentityLevel)Math.Min(
            (int)salesmanThresholds.RequiredIdentityLevel,
            (int)shipperThresholds.RequiredIdentityLevel);

        var query = _dbContext.Customers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.IsActive
                && c.TenantId == new TenantId(tenantId)
                && c.IdentityLevel >= requiredLevelForList
                && c.LoyaltyPoints >= minPointsForList);

        var total = await query.CountAsync();

        var customers = await query
            .OrderByDescending(c => c.LoyaltyPoints)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.FullName,
                c.PhoneNumber,
                c.LoyaltyPoints,
                IdentityLevel = c.IdentityLevel.ToString()
            })
            .ToListAsync();

        var customerIds = customers.Select(c => c.Id).ToList();
        var roles = await _dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => customerIds.Contains(r.CustomerId) && r.IsActive)
            .Select(r => new { r.CustomerId, RoleType = r.RoleType.ToString() })
            .ToListAsync();

        var rolesByCustomer = roles.GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.RoleType).ToList());

        var items = customers.Select(c => new EligibleCustomerDto
        {
            CustomerId = c.Id,
            FullName = c.FullName,
            PhoneNumber = MaskPhone(c.PhoneNumber),
            LoyaltyPoints = c.LoyaltyPoints,
            IdentityLevel = c.IdentityLevel,
            ExistingRoles = rolesByCustomer.TryGetValue(c.Id, out var existing) ? existing : new List<string>()
        }).ToList();

        return new PagedResult<EligibleCustomerDto> { Total = total, Items = items };
    }

    public async Task<CommunityRole> ActivateRoleForTenantAsync(Guid tenantId, Guid customerId, CommunityRoleType role, Guid activatedBy)
    {
        // 1. Verify customer exists + belongs to calling tenant (IDOR guard)
        var customer = await _dbContext.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer == null)
            throw new InvalidOperationException($"Customer {customerId} not found.");

        if (customer.TenantId.Value != tenantId)
            throw new UnauthorizedAccessException(
                $"Customer {customerId} does not belong to tenant {tenantId}.");

        if (!customer.IsActive)
            throw new InvalidOperationException($"Customer {customerId} is not active.");

        // R2.1: Per-tenant eligibility thresholds (replaces hard-coded 1000/Verified)
        var thresholds = await GetEligibilityThresholdsAsync(tenantId, role);
        if (customer.IdentityLevel < thresholds.RequiredIdentityLevel
            || customer.LoyaltyPoints < thresholds.MinPoints)
            throw new InvalidOperationException(
                $"Customer {customerId} does not meet eligibility criteria for {role} " +
                $"(required: IdentityLevel>={thresholds.RequiredIdentityLevel}, LoyaltyPoints>={thresholds.MinPoints}; " +
                $"actual: IdentityLevel={customer.IdentityLevel}, LoyaltyPoints={customer.LoyaltyPoints}).");

        // 2. Check no active role of same type
        var existingRole = await _dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.CustomerId == customerId
                && r.RoleType == role
                && r.IsActive);

        if (existingRole != null)
            throw new InvalidOperationException(
                $"Customer {customerId} already has an active {role} role.");

        // 3. Create role (uses customer.TenantId which == tenantId after IDOR guard)
        var newRole = new CommunityRole(customer.TenantId, customerId, role, activatedBy);
        _dbContext.CommunityRoles.Add(newRole);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "ActivateRoleForTenantAsync: {Role} activated for customer {CustomerId} of tenant {TenantId} by {ActivatedBy}",
            role, customerId, tenantId, activatedBy);

        return newRole;
    }

    public async Task DeactivateRoleForTenantAsync(Guid tenantId, Guid customerId, CommunityRoleType role)
    {
        var existingRole = await _dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.CustomerId == customerId
                && r.RoleType == role
                && r.IsActive);

        if (existingRole == null)
            throw new InvalidOperationException(
                $"No active {role} role found for customer {customerId}.");

        if (existingRole.TenantId.Value != tenantId)
            throw new UnauthorizedAccessException(
                $"Role for customer {customerId} does not belong to tenant {tenantId}.");

        existingRole.Deactivate();
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "DeactivateRoleForTenantAsync: {Role} deactivated for customer {CustomerId} of tenant {TenantId}",
            role, customerId, tenantId);
    }

    public async Task<List<CommunityRole>> GetCustomerRolesForTenantAsync(Guid tenantId, Guid customerId)
    {
        // Verify customer belongs to tenant (IDOR guard)
        var customer = await _dbContext.Customers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer == null)
            throw new InvalidOperationException($"Customer {customerId} not found.");

        if (customer.TenantId.Value != tenantId)
            throw new UnauthorizedAccessException(
                $"Customer {customerId} does not belong to tenant {tenantId}.");

        return await _dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.ActivatedAt)
            .ToListAsync();
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 4) return phone;
        return phone.Substring(0, 3) + "***" + phone[^2..];
    }
}
