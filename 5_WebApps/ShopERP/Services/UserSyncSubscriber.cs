using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client;
using System.Text;
using System.Text.Json;
using VanAn.Shared.Domain;
using VanAn.ShopERP.Infrastructure;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;
using UserTenant = VanAn.Shared.Domain.Aggregates.UserAggregate.UserTenant;
using UserTrustLevel = VanAn.Shared.Domain.Aggregates.UserAggregate.UserTrustLevel;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// User sync PG → SQLite (2026-11-09, RV Self-Serve Onboarding P5):
    /// ShopERP login (Login.cshtml.cs) đọc DemoUser/UserTenant từ SQLite
    /// (IVanAnDbContext → ShopERPDbContext), còn owner user do verify-signup /
    /// onboarding tạo nằm trong PG (VanAnDbContext). Không có sync → owner mới
    /// verify không login được ("Email hoặc password không đúng").
    ///
    /// Subscribed subject (PG→SQLite direction, prefix "cloud"):
    /// - vanan.cloud.user.upserted → upsert DemoUser + UserTenant trong SQLite.
    ///   Payload: { UserId, TenantId, Username, PasswordHash, DisplayName, Role, IsActive }
    ///   (PasswordHash = bcrypt hash — KHÔNG plaintext; NATS internal VPC).
    ///
    /// Global subject (không routing key) — giống TenantSyncSubscriber: user
    /// cần login được trên mọi ShopERP instance host tenant đó.
    /// Idempotent: upsert theo UserId → Username — an toàn NATS redelivery.
    /// </summary>
    public class UserSyncSubscriber : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserSyncSubscriber> _logger;
        private IConnection? _subscriptionConnection;

        public UserSyncSubscriber(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<UserSyncSubscriber> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            const string upsertedSubject = "vanan.cloud.user.upserted";

            string url = _configuration.GetValue<string>("Nats:Url")
                ?? _configuration.GetValue<string>("NATS:Url")
                ?? _configuration.GetValue<string>("NATS__Url")
                ?? _configuration.GetValue<string>("ConnectionStrings:Nats")
                ?? "nats://localhost:4222";

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
                    opts.Name = "vanan-shoperp-user-sync-subscriber";

                    _subscriptionConnection = new ConnectionFactory().CreateConnection(opts);

                    _ = _subscriptionConnection.SubscribeAsync(upsertedSubject, async (sender, args) =>
                    {
                        await SyncUserUpsertedAsync(args.Message.Data, stoppingToken);
                    });

                    _logger.LogInformation(
                        "UserSyncSubscriber connected to NATS {Url}, subscribed to {Subject}",
                        url, upsertedSubject);

                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "UserSyncSubscriber: NATS connection failed at {Url}. Retry in {Delay}ms",
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

            _logger.LogInformation("UserSyncSubscriber stopped.");
        }

        private async Task SyncUserUpsertedAsync(byte[] data, CancellationToken ct)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var userId = root.GetProperty("UserId").GetGuid();
                var tenantId = root.GetProperty("TenantId").GetGuid();
                var username = root.GetProperty("Username").GetString() ?? string.Empty;
                var passwordHash = root.GetProperty("PasswordHash").GetString() ?? string.Empty;
                var displayName = root.GetProperty("DisplayName").GetString() ?? username;
                var role = root.TryGetProperty("Role", out var roleEl) && roleEl.ValueKind == JsonValueKind.Number
                    ? (UserRole)roleEl.GetInt32()
                    : UserRole.Owner;
                var isActive = !root.TryGetProperty("IsActive", out var actEl) || actEl.ValueKind != JsonValueKind.False;

                if (userId == Guid.Empty || tenantId == Guid.Empty || string.IsNullOrWhiteSpace(username))
                {
                    _logger.LogWarning("UserSyncSubscriber: payload thiếu field bắt buộc — skip");
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
                var tenantIdVo = new TenantId(tenantId); // VanAn.Shared.Domain.TenantId VO

                // Match theo Id trước (cross-DB identity), fallback Username+TenantId
                var existing = await dbContext.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == userId, ct)
                    ?? await dbContext.Users
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(u => u.TenantId == tenantIdVo
                            && u.Username.ToLower() == username.ToLower(), ct);

                DemoUser user;
                if (existing is null)
                {
                    // Username là định danh login GLOBAL — chặn insert trùng username
                    // (tenant khác) hoặc trùng PlatformUser (vd sysadmin@vanan.vn),
                    // vì subscriber bypass UserManagementService.CreateUserAsync.
                    var usernameLower = username.ToLower();
                    var usernameTaken = await dbContext.Users
                        .IgnoreQueryFilters()
                        .AnyAsync(u => u.Id != userId && u.Username.ToLower() == usernameLower, ct);
                    var platformTaken = await dbContext.PlatformUsers
                        .IgnoreQueryFilters()
                        .AnyAsync(u => u.Username.ToLower() == usernameLower, ct);
                    if (usernameTaken || platformTaken)
                    {
                        _logger.LogError(
                            "UserSyncSubscriber: SKIP user {Username} ({UserId}) tenant {TenantId} — username đã tồn tại (tenant khác hoặc platform admin). Cần rename ở Gateway trước khi sync.",
                            username, userId, tenantId);
                        return;
                    }

                    user = new DemoUser(tenantIdVo, username, passwordHash, displayName, role);
                    dbContext.Users.Add(user);
                    // Single-identity: giữ Id trùng PG để UserTenant/FK đồng nhất 2 DB
                    dbContext.Entry(user).Property("Id").CurrentValue = userId;
                }
                else
                {
                    user = existing;
                    if (user.PasswordHash != passwordHash)
                        user.ChangePassword(passwordHash);
                    if (user.Role != role)
                        user.AssignRole(role);
                    if (user.DisplayName != displayName)
                        user.UpdateProfile(displayName);
                    if (user.IsActive != isActive)
                    {
                        if (isActive) user.Reactivate(); else user.Deactivate();
                    }
                }

                // ID-P1 (2026-11-10): sync TrustLevel (int) — optional, backward compat.
                // Payload cũ không có field → skip (default None). KHÔNG sync PhoneNumber (PII, PG-only).
                if (root.TryGetProperty("TrustLevel", out var trustEl)
                    && trustEl.ValueKind == JsonValueKind.Number)
                {
                    user.ApplyTrustLevel((UserTrustLevel)trustEl.GetInt32());
                }

                // UserTenant mapping — login fallback tenantId + phân quyền per-tenant
                var mapping = await dbContext.UserTenants
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantIdValue == tenantId, ct);
                if (mapping is null)
                {
                    dbContext.UserTenants.Add(new UserTenant(userId, tenantId, role));
                }
                else if (!mapping.IsActive)
                {
                    mapping.Reactivate();
                }

                await dbContext.SaveChangesAsync(ct);
                var action = existing is null ? "inserted" : "updated";
                _logger.LogInformation(
                    "UserSyncSubscriber: synced user {Username} ({UserId}) tenant {TenantId} → SQLite {Action}",
                    username, userId, tenantId, action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UserSyncSubscriber: failed to process UserUpsertedEvent");
            }
        }
    }
}
