using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Tenant lifecycle management — Wave 5.
    /// Orchestrates: domain aggregate creation → persistence → domain event dispatch → notification.
    /// </summary>
    public class TenantManagementService(
        IVanAnDbContext dbContext,
        INotificationService notificationService,
        ILogger<TenantManagementService> logger) : ITenantManagementService
    {
        public async Task<Tenant> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default)
        {
            var id = new TenantId(Guid.NewGuid());
            var settings = new TenantSettings(
                request.ContactEmail,
                request.ContactPhone,
                request.Address,
                taxCode: request.TaxCode);

            Tenant tenant = request.BusinessType == BusinessType.HouseholdBusiness && request.HKDGroup.HasValue
                ? Tenant.CreateHouseholdBusiness(id, request.Name, request.HKDGroup.Value, settings)
                : Tenant.CreateCompany(id, request.Name, settings);

            // Fix Nhóm 1A: classify Company tenant for VAS feature flag routing.
            // CreateCompany() doesn't set Type (by design — W8 SetTenantType method exists for this).
            // Without this, VasFeatureFlagService.CanAccessVasReportsAsync returns false (Type=null → access denied).
            if (request.BusinessType == BusinessType.Company)
            {
                tenant.SetTenantType(TenantType.Enterprise_SME, AccountingStandard.TT133_2016);
            }

            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync(ct);

            // Dispatch domain events after successful persist
            foreach (var domainEvent in tenant.DomainEvents)
            {
                if (domainEvent is TenantCreatedEvent created)
                    await HandleTenantCreatedAsync(created);
            }
            tenant.ClearDomainEvents();

            logger.LogInformation("Tenant created: {TenantId} ({Name})", id.Value, request.Name);
            return tenant;
        }

        public async Task<Tenant?> GetTenantByIdAsync(TenantId id, CancellationToken ct = default)
            => await dbContext.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == id, ct);

        public async Task<IReadOnlyList<Tenant>> ListTenantsAsync(CancellationToken ct = default)
            => await dbContext.Tenants
                .IgnoreQueryFilters()
                .OrderBy(t => t.Name)
                .ToListAsync(ct);

        public async Task UpdateProfileAsync(TenantId id, UpdateTenantProfileRequest request, CancellationToken ct = default)
        {
            var tenant = await GetTenantByIdAsync(id, ct)
                ?? throw new KeyNotFoundException($"Tenant {id.Value} not found.");

            var settings = new TenantSettings(
                request.ContactEmail,
                request.ContactPhone,
                request.Address,
                taxCode: request.TaxCode,
                slug: tenant.Settings?.Slug); // preserve existing slug — updated via dedicated UpdateSlugAsync

            tenant.UpdateProfile(request.Name, settings);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Tenant profile updated: {TenantId}", id.Value);
        }

        /// <summary>
        /// Tenant Profile Page (2026-07-21): Update URL slug for /store/{slug} route.
        /// Slug uniqueness is enforced by DB unique index (TenantConfiguration).
        /// Throws DbUpdateException if slug already taken by another tenant.
        /// </summary>
        public async Task UpdateSlugAsync(TenantId id, string? slug, CancellationToken ct = default)
        {
            var tenant = await GetTenantByIdAsync(id, ct)
                ?? throw new KeyNotFoundException($"Tenant {id.Value} not found.");

            tenant.UpdateSlug(slug);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Tenant slug updated: {TenantId} -> {Slug}", id.Value, slug ?? "(null)");
        }

        public async Task SuspendAsync(TenantId id, string reason, CancellationToken ct = default)
        {
            var tenant = await GetTenantByIdAsync(id, ct)
                ?? throw new KeyNotFoundException($"Tenant {id.Value} not found.");

            tenant.Suspend(reason);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Tenant suspended: {TenantId}. Reason: {Reason}", id.Value, reason);
        }

        public async Task ReactivateAsync(TenantId id, CancellationToken ct = default)
        {
            var tenant = await GetTenantByIdAsync(id, ct)
                ?? throw new KeyNotFoundException($"Tenant {id.Value} not found.");

            tenant.Reactivate();
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Tenant reactivated: {TenantId}", id.Value);
        }

        public async Task DeactivateAsync(TenantId id, string reason, CancellationToken ct = default)
        {
            var tenant = await GetTenantByIdAsync(id, ct)
                ?? throw new KeyNotFoundException($"Tenant {id.Value} not found.");

            tenant.Deactivate(reason);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Tenant deactivated: {TenantId}. Reason: {Reason}", id.Value, reason);
        }

        /// <summary>Phase 6: Assign tenant to a ShopERP hosting instance (multi-VPS routing).</summary>
        public async Task AssignShopInstanceAsync(TenantId id, Guid shopInstanceId, CancellationToken ct = default)
        {
            if (shopInstanceId == Guid.Empty)
                throw new ArgumentException("ShopInstanceId cannot be empty.", nameof(shopInstanceId));

            var tenant = await GetTenantByIdAsync(id, ct)
                ?? throw new KeyNotFoundException($"Tenant {id.Value} not found.");

            tenant.AssignToShopInstance(shopInstanceId);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Tenant {TenantId} assigned to ShopInstance {ShopInstanceId}",
                id.Value, shopInstanceId);
        }

        // ── Private event handlers ─────────────────────────────────────────────

        private async Task HandleTenantCreatedAsync(TenantCreatedEvent evt)
        {
            if (string.IsNullOrWhiteSpace(evt.ContactEmail))
                return;

            try
            {
                var subject = $"Chào mừng {evt.TenantName} đến với Vạn An!";
                var body = WelcomeEmailBody(evt.TenantName, evt.ContactEmail);
                await notificationService.SendEmailAsync(evt.ContactEmail, subject, body);
            }
            catch (Exception ex)
            {
                // Email failure must NOT roll back the tenant creation
                logger.LogWarning(ex, "Welcome email failed for tenant {TenantId}", evt.TenantId);
            }
        }

        private static string WelcomeEmailBody(string tenantName, string email)
            => $"""
                <html>
                <body>
                  <h2>Chào mừng {tenantName} đến với Vạn An!</h2>
                  <p>Tài khoản của bạn đã được tạo thành công.</p>
                  <p>Email đăng nhập: <strong>{email}</strong></p>
                  <p>Vui lòng liên hệ support@vanan.vn nếu cần hỗ trợ.</p>
                </body>
                </html>
                """;
    }
}
