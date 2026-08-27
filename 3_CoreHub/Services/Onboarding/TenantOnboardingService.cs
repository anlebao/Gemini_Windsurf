using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
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
    ///
    /// Crawl-to-Onboard Pipeline (2026-08-25): Extended with OnboardUnverifiedAsync
    /// (Pending only, no user/groups) + VerifyAsync (Pending → Active + user + groups
    /// + Option A outbox publish for NATS sync sang SQLite).
    /// </summary>
    public class TenantOnboardingService(
        ITenantManagementService tenantService,
        IUserManagementService userService,
        IPermissionGroupService permissionGroupService,
        IRoleAssignmentService roleAssignmentService,
        IVanAnDbContext dbContext,
        IOutboxRepository? outboxRepository,
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

            // ── 1b. Phase 6: Assign tenant to ShopERP instance (multi-VPS routing) ──
            if (request.ShopInstanceId.HasValue && request.ShopInstanceId.Value != Guid.Empty)
            {
                await tenantService.AssignShopInstanceAsync(tenantId, request.ShopInstanceId.Value, ct);
                logger.LogInformation("Tenant {TenantId} assigned to ShopInstance {ShopInstanceId}",
                    tenantId.Value, request.ShopInstanceId.Value);
            }

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

        // ── Crawl-to-Onboard Pipeline (2026-08-25) ──────────────────────────

        /// <summary>
        /// Crawl-to-Onboard: Creates a Pending tenant from crawled business listing.
        /// NO owner user, NO permission groups, NO welcome email.
        /// SĐT section HIDDEN on profile (M3 — CrawledPhone stored internal, ContactPhone=null).
        /// </summary>
        public async Task<Guid> OnboardUnverifiedAsync(
            CrawlListingDto listing,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(listing.Name);
            ArgumentException.ThrowIfNullOrWhiteSpace(listing.SourceSite);
            ArgumentException.ThrowIfNullOrWhiteSpace(listing.SourceUrl);

            // Generate tenant ID (UUIDv7 via Guid.NewGuid — matches existing pattern)
            var tenantId = new TenantId(Guid.NewGuid());

            // Build settings: CrawledPhone stored internal, ContactPhone=null (M3 — SĐT hidden on Pending profile)
            var settings = new TenantSettings(
                contactEmail: null,        // No email from crawl (doanhnghiep.vn doesn't return email)
                contactPhone: null,        // M3: ContactPhone=null on Pending (SĐT section hidden)
                address: listing.Address,
                taxCode: listing.TaxCode,
                latitude: listing.Lat,
                longitude: listing.Lng,
                crawledPhone: listing.CrawledPhone);  // M3: internal use only

            // Auto-generate pending slug: pending-{taxCode ?? random}-{random4hex}
            // Retry on collision (rare — unique index on Settings.Slug)
            string pendingSlug = GeneratePendingSlug(listing.TaxCode);
            var tenant = Tenant.CreateUnverified(tenantId, listing.Name, settings, pendingSlug);

            // Save CrawlSource audit row for provenance
            var crawlSource = CrawlSource.Create(
                tenantId,
                listing.SourceSite,
                listing.SourceUrl,
                JsonSerializer.Serialize(listing));

            // Check for duplicate MST — correction H5: first canonical tenant kept,
            // rest get PotentialDuplicateOf = canonical.Id (not chain of duplicates).
            // Query existing tenant by TaxCode (Active OR Pending — both are "canonical" candidates).
            if (!string.IsNullOrWhiteSpace(listing.TaxCode))
            {
                var existing = await dbContext.Tenants
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.Settings.TaxCode == listing.TaxCode
                        && t.Id != tenantId, ct);
                if (existing is not null)
                {
                    tenant.MarkPotentialDuplicateOf(existing.Id.Value);
                    logger.LogInformation(
                        "Pending tenant {TenantId} marked as potential duplicate of {ExistingTenantId} (same MST {TaxCode})",
                        tenantId.Value, existing.Id.Value, listing.TaxCode);
                }
            }

            // Save tenant + crawl source
            dbContext.Tenants.Add(tenant);
            dbContext.CrawlSources.Add(crawlSource);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Pending tenant created from crawl: {TenantId} ({Name}) — source {SourceSite}",
                tenantId.Value, listing.Name, listing.SourceSite);

            return tenantId.Value;
        }

        /// <summary>
        /// Crawl-to-Onboard: Verifies a Pending tenant → Active.
        /// Creates owner user + Owner role + 4 default permission groups.
        /// Sets ContactPhone from owner-provided form (M3 — consented, NOT from CrawledPhone).
        /// Updates slug to clean slug (now UpdateSlug works — tenant is Active).
        /// Option A: Publishes OutboxMessage TenantVerifiedEvent → NATS sync sang SQLite.
        /// </summary>
        public async Task<VerifyResult> VerifyAsync(
            Guid tenantId,
            VerifyTenantRequest req,
            CancellationToken ct = default)
        {
            var tenantIdVo = new TenantId(tenantId);
            var tenant = await dbContext.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantIdVo, ct)
                ?? throw new KeyNotFoundException($"Pending tenant {tenantId} not found.");

            // 1. Verify tenant: Pending → Active (domain guard checks Status + PotentialDuplicateOf)
            tenant.Verify();

            // 2. Set ContactPhone from owner-provided form (M3 — consented, NOT from CrawledPhone)
            if (!string.IsNullOrWhiteSpace(req.OwnerPhone) || !string.IsNullOrWhiteSpace(req.OwnerEmail))
            {
                var settingsWithContact = tenant.Settings;
                if (!string.IsNullOrWhiteSpace(req.OwnerPhone))
                    settingsWithContact = settingsWithContact.WithContactPhone(req.OwnerPhone);
                if (!string.IsNullOrWhiteSpace(req.OwnerEmail))
                    settingsWithContact = settingsWithContact.WithContactEmail(req.OwnerEmail);
                tenant.UpdateProfile(tenant.Name, settingsWithContact);
            }

            // 3. Update slug to clean slug (now UpdateSlug works — tenant is Active)
            string publishedSlug = req.Slug;
            if (string.IsNullOrWhiteSpace(publishedSlug))
                publishedSlug = Slugify(tenant.Name);
            tenant.UpdateSlug(publishedSlug);

            // 4. Assign to ShopInstance if provided (Multi-VPS routing)
            if (req.ShopInstanceId.HasValue && req.ShopInstanceId.Value != Guid.Empty)
            {
                tenant.AssignToShopInstance(req.ShopInstanceId.Value);
            }

            // 5. Create owner user
            var ownerUser = await userService.CreateUserAsync(
                tenantIdVo,
                req.OwnerUsername,
                req.OwnerPassword,
                req.OwnerDisplayName,
                UserRole.Owner,
                ct);

            // 6. Assign Owner role
            await roleAssignmentService.AssignRoleToUserAsync(ownerUser.Id, tenantIdVo, UserRole.Owner, ct);

            // 7. Create 4 default permission groups + assign owner to Quản lý
            var createdGroups = new List<Guid>(DefaultGroups.Length);
            foreach (var (name, description) in DefaultGroups)
            {
                var group = await permissionGroupService.CreateGroupAsync(tenantIdVo, name, description, ct);
                createdGroups.Add(group.Id);
            }
            if (createdGroups.Count > 0)
            {
                await roleAssignmentService.AssignUserToGroupAsync(ownerUser.Id, createdGroups[0], tenantIdVo, ct);
            }

            // 8. Save all changes (tenant + user + groups — atomic)
            await dbContext.SaveChangesAsync(ct);

            // 9. Option A: Publish TenantVerifiedEvent to Outbox → NATS → TenantSyncSubscriber upserts SQLite row
            //    (data integrity: same Guid tenantId in both PG + SQLite → avoids accounting split)
            await PublishTenantVerifiedEventAsync(tenant, req.ApprovedByUserId, ct);

            logger.LogInformation(
                "Tenant {TenantId} verified: Pending → Active. Owner user {OwnerUserId}. Slug: {Slug}. Groups: {GroupCount}.",
                tenantId, ownerUser.Id, publishedSlug, createdGroups.Count);

            return new VerifyResult(
                TenantId: tenantId,
                OwnerUserId: ownerUser.Id,
                PermissionGroupsCreated: createdGroups.Count,
                PublishedSlug: publishedSlug);
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private static string GeneratePendingSlug(string? taxCode)
        {
            var prefix = !string.IsNullOrWhiteSpace(taxCode)
                ? taxCode.Trim().ToLowerInvariant()
                : Guid.NewGuid().ToString("N")[..8];
            var random4 = Guid.NewGuid().ToString("N")[..4];
            return $"pending-{prefix}-{random4}";
        }

        private static string Slugify(string name)
        {
            // Simple slugify: lowercase, remove diacritics, replace spaces with hyphens
            var slug = name.Trim().ToLowerInvariant();
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^\w\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');
            return string.IsNullOrWhiteSpace(slug) ? $"tenant-{Guid.NewGuid():N}"[..30] : slug[..Math.Min(slug.Length, 100)];
        }

        private async Task PublishTenantVerifiedEventAsync(
            Tenant tenant,
            Guid approvedByUserId,
            CancellationToken ct)
        {
            if (outboxRepository is null)
            {
                logger.LogWarning("OutboxRepository not available — TenantVerifiedEvent not published for tenant {TenantId}", tenant.Id.Value);
                return;
            }

            // Issue #165: include TenantName + Settings snapshot so TenantSyncSubscriber creates
            // the SQLite row with the correct name (not placeholder "(synced from Gateway)").
            var evt = new TenantVerifiedEvent(
                tenant.Id.Value,
                approvedByUserId,
                DateTime.UtcNow,
                tenant.Name,
                TenantSettingsSnapshot.From(tenant.Settings));
            var eventData = JsonSerializer.Serialize(evt);
            var outboxEvent = new OutboxEvent(
                tenant.TenantId,
                new ElectronicInvoiceId(Guid.Empty),
                "TenantVerified",
                eventData,
                correlationId: tenant.Id.Value);

            await outboxRepository.EnqueueAsync(outboxEvent, ct);
            logger.LogInformation(
                "Enqueued TenantVerifiedEvent to Outbox for tenant {TenantId} (EventId={EventId})",
                tenant.Id.Value, evt.EventId);
        }
    }
}
