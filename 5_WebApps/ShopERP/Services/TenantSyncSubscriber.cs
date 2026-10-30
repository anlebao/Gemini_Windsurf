using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client;
using System.Text;
using System.Text.Json;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.ShopERP.Infrastructure;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Crawl-to-Onboard Pipeline (2026-08-25, Option A): Subscribes to NATS tenant events
    /// published by Gateway's NatsSyncWorker + syncs tenant data from PostgreSQL (Gateway)
    /// → SQLite (ShopERP) to ensure tenant identity consistency (same Guid tenantId in both DBs).
    ///
    /// Without this, ProductsController.cs:146 + UserManagement.razor.cs:45 (which query
    /// ShopERP SQLite Tenants table) would show stale/missing data for tenants created via
    /// Gateway (Crawl-to-Onboard Verify) or updated via Gateway (admin profile update).
    ///
    /// Data integrity constraint (user-raised 2026-08-25): if tenant has 2 different IDs in
    /// PG vs SQLite → order/accounting splits between 2 tenant IDs → số liệu kế toán sai.
    ///
    /// Subscribed subjects (PG→SQLite direction, prefix "cloud" = Gateway publishes):
    /// - vanan.cloud.tenant.verified → upsert Tenant row in SQLite (Pending → Active transition)
    /// - vanan.cloud.tenant.profile.updated → update Tenant row in SQLite (admin profile update)
    ///
    /// NOT subscribed:
    /// - vanan.cloud.tenant.pending → Pending tenants NOT synced (no business activity, no orders/accounting)
    ///
    /// Idempotent: re-delivery of same event upserts (not inserts duplicate) — safe for NATS redelivery.
    /// </summary>
    public class TenantSyncSubscriber : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TenantSyncSubscriber> _logger;
        private IConnection? _subscriptionConnection;

        public TenantSyncSubscriber(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<TenantSyncSubscriber> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Tenant sync is global — all ShopERP instances need all tenant data
            // (unlike OrderSyncSubscriber which routes by ShopInstanceId).
            const string verifiedSubject = "vanan.cloud.tenant.verified";
            const string profileUpdatedSubject = "vanan.cloud.tenant.profile.updated";
            // 2026-10-15 (fix báo cáo HTX): MarkAsHtx/revert chỉ ghi PG → event này cập nhật
            // SQLite mirror Type/AccountingStandard cho khớp PG (source of truth).
            const string typeChangedSubject = "vanan.cloud.tenant.type.changed";

            string url = _configuration.GetValue<string>("Nats:Url")
                ?? _configuration.GetValue<string>("NATS:Url")
                ?? _configuration.GetValue<string>("NATS__Url")
                ?? _configuration.GetValue<string>("ConnectionStrings:Nats")
                ?? "nats://localhost:4222";

            // Retry loop: NATS may be temporarily unavailable at startup
            int retryDelay = 2000;
            const int maxRetryDelay = 30000;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var opts = ConnectionFactory.GetDefaultOptions();
                    opts.Url = url;
                    opts.MaxReconnect = 5;
                    opts.ReconnectWait = 2000;
                    opts.Name = "vanan-shoperp-tenant-sync-subscriber";

                    _subscriptionConnection = new ConnectionFactory().CreateConnection(opts);

                    _ = _subscriptionConnection.SubscribeAsync(verifiedSubject, async (sender, args) =>
                    {
                        await SyncTenantVerifiedAsync(args.Message.Data, stoppingToken);
                    });

                    _ = _subscriptionConnection.SubscribeAsync(profileUpdatedSubject, async (sender, args) =>
                    {
                        await SyncTenantProfileUpdatedAsync(args.Message.Data, stoppingToken);
                    });

                    _ = _subscriptionConnection.SubscribeAsync(typeChangedSubject, async (sender, args) =>
                    {
                        await SyncTenantTypeChangedAsync(args.Message.Data, stoppingToken);
                    });

                    _logger.LogInformation(
                        "TenantSyncSubscriber connected to NATS {Url}, subscribed to {Verified} + {ProfileUpdated} + {TypeChanged}",
                        url, verifiedSubject, profileUpdatedSubject, typeChangedSubject);

                    // Connected successfully — wait indefinitely until cancelled
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "TenantSyncSubscriber: NATS connection failed at {Url}. Retry in {Delay}ms",
                        url, retryDelay);
                }

                try
                {
                    await Task.Delay(retryDelay, stoppingToken);
                    retryDelay = Math.Min(retryDelay * 2, maxRetryDelay);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("TenantSyncSubscriber stopped.");
        }

        /// <summary>
        /// Handle TenantVerifiedEvent — upsert Tenant row in SQLite.
        /// Tenant transitions Pending → Active. Create or update SQLite row with same Guid.
        ///
        /// Issue #165 (2026-08-27): Event now carries TenantName + Settings snapshot.
        /// If present, use UpsertTenantWithSnapshotAsync (correct name + settings).
        /// If absent (legacy event), fall back to UpsertTenantRowAsync (minimal row).
        /// </summary>
        private async Task SyncTenantVerifiedAsync(byte[] data, CancellationToken ct)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                using var doc = JsonDocument.Parse(json);

                var tenantId = doc.RootElement.GetProperty("TenantId").GetGuid();

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();

                // Issue #165: check if event carries full data (TenantName + Settings)
                if (doc.RootElement.TryGetProperty("Settings", out var settingsElem) && settingsElem.ValueKind == JsonValueKind.Object)
                {
                    var tenantName = doc.RootElement.TryGetProperty("TenantName", out var nameElem)
                        ? (nameElem.GetString() ?? string.Empty)
                        : string.Empty;
                    var snapshot = JsonSerializer.Deserialize<TenantSettingsSnapshot>(settingsElem.GetRawText());

                    await UpsertTenantWithSnapshotAsync(dbContext, tenantId, tenantName, snapshot, ct);
                    _logger.LogInformation(
                        "TenantSyncSubscriber: synced TenantVerifiedEvent for tenant {TenantId} ({Name}) → SQLite upserted with full data",
                        tenantId, tenantName);
                }
                else
                {
                    // Legacy event (no Settings) — upsert minimal row
                    await UpsertTenantRowAsync(dbContext, tenantId, ct);
                    _logger.LogInformation(
                        "TenantSyncSubscriber: synced TenantVerifiedEvent for tenant {TenantId} → SQLite upserted (minimal, no snapshot)",
                        tenantId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TenantSyncSubscriber: failed to process TenantVerifiedEvent");
            }
        }

        /// <summary>
        /// Handle TenantProfileUpdatedEvent — update Tenant row in SQLite with new name + settings snapshot.
        /// </summary>
        private async Task SyncTenantProfileUpdatedAsync(byte[] data, CancellationToken ct)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                using var doc = JsonDocument.Parse(json);

                var tenantId = doc.RootElement.GetProperty("TenantId").GetGuid();
                var newName = doc.RootElement.GetProperty("NewName").GetString() ?? string.Empty;

                // Parse settings snapshot
                var settingsElem = doc.RootElement.GetProperty("Settings");
                var snapshot = JsonSerializer.Deserialize<TenantSettingsSnapshot>(settingsElem.GetRawText());

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();

                await UpsertTenantWithSnapshotAsync(dbContext, tenantId, newName, snapshot, ct);

                _logger.LogInformation(
                    "TenantSyncSubscriber: synced TenantProfileUpdatedEvent for tenant {TenantId} ({Name}) → SQLite updated",
                    tenantId, newName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TenantSyncSubscriber: failed to process TenantProfileUpdatedEvent");
            }
        }

        /// <summary>
        /// Upsert tenant row in SQLite — create if not exists, update if exists.
        /// Minimal version (just Id + Status=Active) — used for TenantVerifiedEvent.
        /// </summary>
        private async Task UpsertTenantRowAsync(ShopERPDbContext dbContext, Guid tenantId, CancellationToken ct)
        {
            var tenantIdVo = new TenantId(tenantId);
            var existing = await dbContext.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantIdVo, ct);

            if (existing is null)
            {
                // Create minimal tenant row in SQLite
                // Use CreateCompany factory (sets Status=Active, raises TenantCreatedEvent —
                // but we clear events since this is a sync, not a new creation)
                var tenant = Tenant.CreateCompany(tenantIdVo, "(synced from Gateway)", TenantSettings.Empty());
                tenant.ClearDomainEvents();  // Don't dispatch events for sync
                dbContext.Tenants.Add(tenant);
                // Set TenantId = own Id (multi-tenancy discriminator for self-reference)
                dbContext.Entry(tenant).Property("TenantId").CurrentValue = tenantIdVo;
            }
            else
            {
                // Already exists — ensure Status is Active
                // (idempotent — re-delivery of verified event is safe)
            }

            await dbContext.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Upsert tenant row in SQLite with full settings snapshot — used for TenantProfileUpdatedEvent.
        /// </summary>
        private async Task UpsertTenantWithSnapshotAsync(
            ShopERPDbContext dbContext,
            Guid tenantId,
            string newName,
            TenantSettingsSnapshot? snapshot,
            CancellationToken ct)
        {
            if (snapshot is null) return;

            var tenantIdVo = new TenantId(tenantId);
            var existing = await dbContext.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantIdVo, ct);

            // Build settings from snapshot
            var settings = new TenantSettings(
                contactEmail: snapshot.ContactEmail,
                contactPhone: snapshot.ContactPhone,
                address: snapshot.Address,
                logoUrl: snapshot.LogoUrl,
                taxCode: snapshot.TaxCode,
                latitude: snapshot.Latitude,
                longitude: snapshot.Longitude,
                slug: snapshot.Slug,
                socialLinksFb: snapshot.SocialLinksFb,
                socialLinksTiktok: snapshot.SocialLinksTiktok,
                brandStory: snapshot.BrandStory,
                theme: (ThemeType)snapshot.Theme,
                commerceModeOverride: (CommerceMode)snapshot.CommerceModeOverride,
                navColor: snapshot.NavColor,
                headerColor: snapshot.HeaderColor,
                footerColor: snapshot.FooterColor);

            if (existing is null)
            {
                // Create new tenant row in SQLite with full data from snapshot
                var tenant = Tenant.CreateCompany(tenantIdVo, newName, settings);
                tenant.ClearDomainEvents();  // Sync, not new creation
                dbContext.Tenants.Add(tenant);
                dbContext.Entry(tenant).Property("TenantId").CurrentValue = tenantIdVo;
            }
            else
            {
                // Update existing tenant row with new name + settings
                existing.UpdateProfile(newName, settings);
            }

            await dbContext.SaveChangesAsync(ct);
        }
        /// <summary>
        /// 2026-10-15 (fix báo cáo HTX): handle "vanan.cloud.tenant.type.changed" — MarkAsHtx
        /// (HtxProfileService) hoặc RevertAsync chỉ ghi PG; event này đồng bộ SQLite mirror
        /// Type/AccountingStandard cho khớp PG (source of truth) → UI kế toán resolve đúng TT 71.
        /// Payload: { TenantId, Type (int?), AccountingStandard (int?), UpdatedAt }.
        /// Idempotent: re-delivery an toàn (MarkAsHtx/RevertTenantType đều có guard riêng).
        /// </summary>
        internal async Task SyncTenantTypeChangedAsync(byte[] data, CancellationToken ct)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                using var doc = JsonDocument.Parse(json);

                var tenantId = doc.RootElement.GetProperty("TenantId").GetGuid();
                var type = doc.RootElement.TryGetProperty("Type", out var typeEl) && typeEl.ValueKind == JsonValueKind.Number
                    ? (TenantType?)(TenantType)typeEl.GetInt32()
                    : null;
                var standard = doc.RootElement.TryGetProperty("AccountingStandard", out var stdEl) && stdEl.ValueKind == JsonValueKind.Number
                    ? (AccountingStandard?)(AccountingStandard)stdEl.GetInt32()
                    : null;
                var dnsnTax = doc.RootElement.TryGetProperty("DnsnTaxMethod", out var dnsnEl) && dnsnEl.ValueKind == JsonValueKind.Number
                    ? (DnsnTaxMethod?)(DnsnTaxMethod)dnsnEl.GetInt32()
                    : null;

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();

                var tenantIdVo = new TenantId(tenantId);
                var existing = await dbContext.Tenants
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.Id == tenantIdVo, ct);
                if (existing is null)
                {
                    _logger.LogWarning(
                        "TenantSyncSubscriber: type.changed cho tenant {TenantId} nhưng mirror row không tồn tại — skip (tenant chưa sync qua verified/profile.updated)",
                        tenantId);
                    return;
                }

                if (type == TenantType.HTX)
                {
                    // HTX → TT 71 (MarkAsHtx set standard = TT71 luôn)
                    existing.MarkAsHtx();
                }
                else if (existing.Type == TenantType.HTX && type is not null)
                {
                    // Revert HTX → Công ty/HKD
                    existing.RevertTenantType(type.Value, standard);
                }
                else if (standard != existing.AccountingStandard || dnsnTax != existing.Settings?.DnsnTaxMethod)
                {
                    // Regime-lock (2026-10-30): bầu chọn chuẩn (HKD→TT58 Điều 2.2, siêu nhỏ→TT133)
                    // + DnsnTaxMethod — Type giữ nguyên, chỉ cập nhật override trên mirror.
                    existing.ElectAccountingStandard(standard);
                    existing.SetDnsnTaxMethod(dnsnTax);
                }
                else
                {
                    // Không phải transition liên quan HTX — mirror giữ nguyên
                    _logger.LogDebug("TenantSyncSubscriber: type.changed {TenantId} bỏ qua (mirror Type={MirrorType}, event Type={EventType})",
                        tenantId, existing.Type?.ToString() ?? "null", type?.ToString() ?? "null");
                    return;
                }

                await dbContext.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "TenantSyncSubscriber: tenant {TenantId} SQLite mirror Type → {Type} / Standard → {Standard}",
                    tenantId, existing.Type?.ToString() ?? "null", existing.AccountingStandard?.ToString() ?? "null");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TenantSyncSubscriber: failed to process tenant type.changed event");
            }
        }
    }
}
