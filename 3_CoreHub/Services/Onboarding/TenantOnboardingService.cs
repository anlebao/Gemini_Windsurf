using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.CoreHub.Services.Onboarding
{
    /// <summary>
    /// Orchestrates the full tenant onboarding flow in a single call:
    /// 1. Create tenant  →  2. Create owner user  →  3. Assign Owner role
    /// 4. Seed industry data  →  5. Create default permission groups
    /// 6. Assign owner to Quản lý group
    ///
    /// Wave 3: Full implementation of <see cref="ITenantOnboardingService"/>.
    /// </summary>
    public class TenantOnboardingService(
        ITenantManagementService tenantService,
        IUserManagementService userService,
        IPermissionGroupService permissionGroupService,
        IRoleAssignmentService roleAssignmentService,
        IEnumerable<IIndustrySeedStrategy> seedStrategies,
        IVanAnDbContext dbContext,
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
            // ── 0. Validate industry code ──────────────────────────────────────────
            var strategy = seedStrategies
                .FirstOrDefault(s => string.Equals(s.IndustryCode, request.IndustryCode, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException(
                    $"Industry code '{request.IndustryCode}' is not registered. " +
                    $"Available: {string.Join(", ", seedStrategies.Select(s => s.IndustryCode))}",
                    nameof(request));

            logger.LogInformation(
                "Starting onboarding for tenant '{Name}' with industry '{Industry}'",
                request.Name, strategy.IndustryCode);

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

            // ── 4. Seed industry data ──────────────────────────────────────────────
            var seedResult = await strategy.SeedAsync(tenantId, dbContext, ct);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Industry seed complete for {Industry}: {Shops} shops, {Products} products, {Ingredients} ingredients, {Recipes} recipes",
                strategy.IndustryCode,
                seedResult.ShopsCreated, seedResult.ProductsCreated,
                seedResult.IngredientsCreated, seedResult.RecipesCreated);

            // ── 5. Create default permission groups ────────────────────────────────
            var createdGroups = new List<Guid>(DefaultGroups.Length);
            foreach (var (name, description) in DefaultGroups)
            {
                var group = await permissionGroupService.CreateGroupAsync(tenantId, name, description, ct);
                createdGroups.Add(group.Id);
            }

            // ── 6. Assign owner to "Quản lý" group (first group created) ──────────
            if (createdGroups.Count > 0)
            {
                await roleAssignmentService.AssignUserToGroupAsync(ownerUser.Id, createdGroups[0], tenantId, ct);
                logger.LogInformation(
                    "Owner {UserId} assigned to 'Quản lý' group {GroupId}",
                    ownerUser.Id, createdGroups[0]);
            }

            var warnings = new List<string>(seedResult.Warnings);
            if (warnings.Count > 0)
                logger.LogWarning("Onboarding completed with {WarningCount} seed warnings for tenant {TenantId}", warnings.Count, tenantId.Value);

            logger.LogInformation(
                "Onboarding complete for tenant {TenantId}. Groups: {GroupCount}",
                tenantId.Value, createdGroups.Count);

            return new TenantOnboardingResult(
                TenantId: tenantId.Value,
                OwnerUserId: ownerUser.Id,
                ProductsCreated: seedResult.ProductsCreated,
                IngredientsCreated: seedResult.IngredientsCreated,
                RecipesCreated: seedResult.RecipesCreated,
                ShopsCreated: seedResult.ShopsCreated,
                PermissionGroupsCreated: createdGroups.Count,
                Warnings: warnings.AsReadOnly());
        }
    }
}
