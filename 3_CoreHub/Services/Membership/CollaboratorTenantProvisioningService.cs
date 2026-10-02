using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using TenantAggregate = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Services.Membership;

/// <summary>
/// Membership Infrastructure (2026-10-02, user directive): tự động phát sinh tenant profile
/// cho cộng tác viên (Salesman/Shipper) khi được duyệt làm thành viên HTX.
/// - Tenant KHÔNG loại hình (Type = null — chỉ phục vụ membership, không commerce/accounting).
/// - Status = Active ngay (identity đã OTP-verified qua customer).
/// - OwnerCustomerId = collaborator customer id (Tenant sở hữu bởi collaborator).
/// - Idempotent: 1 tenant / 1 customer (reuse qua OwnerCustomerId) — dùng chung cho nhiều HTX.
/// </summary>
public interface ICollaboratorTenantProvisioningService
{
    /// <summary>Customer có role cộng tác viên đang active (Salesman/Shipper)?</summary>
    Task<bool> IsCollaboratorAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Tenant profile đã tồn tại cho customer (OwnerCustomerId) — null nếu chưa có.</summary>
    Task<TenantId?> GetExistingProfileAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Lấy hoặc tạo tenant profile cho collaborator (idempotent).</summary>
    Task<TenantId> GetOrCreateProfileAsync(
        Guid customerId, string displayName, string? phone = null, string? email = null,
        CancellationToken ct = default);
}

public class CollaboratorTenantProvisioningService(
    IVanAnDbContext dbContext,
    ILogger<CollaboratorTenantProvisioningService> logger) : ICollaboratorTenantProvisioningService
{
    /// <inheritdoc />
    public async Task<bool> IsCollaboratorAsync(Guid customerId, CancellationToken ct = default)
    {
        if (customerId == Guid.Empty) return false;
        return await dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(r => r.CustomerId == customerId
                && r.IsActive
                && !r.IsDeleted
                && (r.RoleType == CommunityRoleType.Salesman || r.RoleType == CommunityRoleType.Shipper), ct);
    }

    /// <inheritdoc />
    public async Task<TenantId?> GetExistingProfileAsync(Guid customerId, CancellationToken ct = default)
    {
        if (customerId == Guid.Empty) return null;
        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.OwnerCustomerId == customerId && !t.IsDeleted, ct);
        return tenant?.Id;
    }

    /// <inheritdoc />
    public async Task<TenantId> GetOrCreateProfileAsync(
        Guid customerId, string displayName, string? phone = null, string? email = null,
        CancellationToken ct = default)
    {
        var existing = await GetExistingProfileAsync(customerId, ct);
        if (existing is not null)
        {
            logger.LogInformation("Collaborator {CustomerId} already has tenant profile {TenantId} — reuse", customerId, existing.Value);
            return existing.Value;
        }

        var tenantId = new TenantId(Guid.NewGuid());
        var settings = new TenantSettings(
            contactEmail: email,
            contactPhone: phone,
            address: null);
        var tenant = TenantAggregate.CreateMembershipProfile(tenantId, displayName, settings);
        tenant.AssignOwnerCustomer(customerId);

        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Auto-provisioned membership tenant profile {TenantId} ({Name}) for collaborator {CustomerId}",
            tenantId.Value, displayName, customerId);
        return tenantId;
    }
}
