using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.CoreHub.Services.Onboarding
{
    /// <summary>
    /// Orchestrates the full tenant onboarding flow in a single call:
    /// 1. Create tenant  →  2. Create owner user  →  3. Assign Owner role
    /// 4. Create default permission groups  →  5. Assign owner to Quản lý group
    ///
    /// Phase 3.6 (Multi-VPS Checkout): Product seeding removed from onboarding.
    /// Gateway PG no longer stores Products (FK dropped in Phase 3, Option C).
    /// Tenant owner runs QuickSetup manually after first login to seed industry data
    /// via ShopERP SQLite (where Products now live).
    /// The IndustryCode field in OnboardTenantRequest is kept for backward API compat
    /// but is no longer used for seeding during onboarding.
    /// </summary>
    public class TenantOnboardingService(
        ITenantManagementService tenantService,
        IUserManagementService userService,
        IPermissionGroupService permissionGroupService,
        IRoleAssignmentService roleAssignmentService,
        ILogger<TenantOnboardingService> logger) : ITenantOnboardingService
    {
        // Default F&B permission groups: name → description
        private static readonly (string Name, string Description)[] DefaultGroups =
        [
            ("Quản lý",   "Quyền quản lý toàn diện cửa hàng — Owner & StoreKeeper"),
            ("Thu ngân",  "Quyền tạo và xử lý đơn hàng, thanh toán — Staff"),
            ("Bếp",       "Quyền xem đơn hàng và thực hiện chế biến — Staff & Masterchef"),
            ("Kho",       "Quyền quản lý kho và nhập xuất nguyên liệu — StoreKeeper"),
        ];

        public async Task<TenantOnboardingResult> OnboardAsync(
            OnboardTenantRequest request,
            CancellationToken ct = default)
        {
            logger.LogInformation(
                "Starting onboarding for tenant '{Name}' (industry code '{Industry}' — seeding deferred to QuickSetup)",
                request.Name, request.IndustryCode);

            // ── 1. Create tenant ───────────────────────────────────────────────────
            var createTenantRequest = new CreateTenantRequest(
                request.Name,
                request.BusinessType,
                request.HKDGroup,
                request.ContactEmail,
                request.ContactPhone,
                request.Address,
                request.TaxCode);

            var tenant = await tenantService.CreateTenantAsync(createTenantRequest, ct);
            var tenantId = tenant.Id;

            logger.LogInformation("Tenant created: {TenantId}", tenantId.Value);

            // ── 2. Create owner user ───────────────────────────────────────────────
            var ownerUser = await userService.CreateUserAsync(
                tenantId,
                request.OwnerUsername,
                request.OwnerPassword,
                request.OwnerDisplayName,
                UserRole.Owner,
                ct);

            logger.LogInformation("Owner user created: {UserId} ({Username})", ownerUser.Id, request.OwnerUsername);

            // ── 3. Assign Owner role via RoleAssignmentService ─────────────────────
            await roleAssignmentService.AssignRoleToUserAsync(ownerUser.Id, tenantId, UserRole.Owner, ct);

            // ── 4. Create default permission groups ────────────────────────────────
            var createdGroups = new List<Guid>(DefaultGroups.Length);
            foreach (var (name, description) in DefaultGroups)
            {
                var group = await permissionGroupService.CreateGroupAsync(tenantId, name, description, ct);
                createdGroups.Add(group.Id);
            }

            // ── 5. Assign owner to "Quản lý" group (first group created) ──────────
            if (createdGroups.Count > 0)
            {
                await roleAssignmentService.AssignUserToGroupAsync(ownerUser.Id, createdGroups[0], tenantId, ct);
                logger.LogInformation(
                    "Owner {UserId} assigned to 'Quản lý' group {GroupId}",
                    ownerUser.Id, createdGroups[0]);
            }

            logger.LogInformation(
                "Onboarding complete for tenant {TenantId}. Groups: {GroupCount}. " +
                "Product seeding deferred — owner runs QuickSetup after first login.",
                tenantId.Value, createdGroups.Count);

            return new TenantOnboardingResult(
                TenantId: tenantId.Value,
                OwnerUserId: ownerUser.Id,
                ProductsCreated: 0,
                IngredientsCreated: 0,
                RecipesCreated: 0,
                ShopsCreated: 0,
                PermissionGroupsCreated: createdGroups.Count,
                Warnings: new List<string>
                {
                    "Product seeding deferred to QuickSetup. Owner must run QuickSetup after first login to seed industry data."
                }.AsReadOnly());
        }
    }
}
