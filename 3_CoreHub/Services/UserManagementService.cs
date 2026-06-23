using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;
using UserCreatedEvent = VanAn.Shared.Domain.Aggregates.UserAggregate.UserCreatedEvent;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// User lifecycle management — Wave 6.
    /// Orchestrates: domain aggregate creation → persistence → domain event dispatch → notification.
    /// </summary>
    public class UserManagementService(
        IVanAnDbContext dbContext,
        INotificationService notificationService,
        ILogger<UserManagementService> logger) : IUserManagementService
    {
        public async Task<DemoUser> CreateUserAsync(
            TenantId tenantId,
            string username,
            string plainPassword,
            string displayName,
            UserRole role,
            CancellationToken ct = default)
        {
            if (tenantId is null || tenantId.Value == Guid.Empty)
                throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty.", nameof(username));
            if (string.IsNullOrWhiteSpace(plainPassword))
                throw new ArgumentException("Password cannot be empty.", nameof(plainPassword));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("DisplayName cannot be empty.", nameof(displayName));

            var existing = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower() && u.TenantId == tenantId, ct);
            if (existing != null)
                throw new InvalidOperationException("Username already exists in this tenant.");

            string passwordHash;
            try
            {
                passwordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword, 12);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to hash password. Please try again.", ex);
            }

            var user = DemoUser.Create(tenantId, username, passwordHash, displayName, role);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(ct);

            foreach (var domainEvent in user.DomainEvents)
            {
                if (domainEvent is UserCreatedEvent created)
                    await HandleUserCreatedAsync(created);
            }
            user.ClearDomainEvents();

            logger.LogInformation("User created: {UserId} ({Username}) in tenant {TenantId}", user.Id, username, tenantId.Value);
            return user;
        }

        public async Task<DemoUser?> GetUserByIdAsync(Guid userId, TenantId tenantId, CancellationToken ct = default)
        {
            var user = await dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user != null && user.TenantId != tenantId)
                throw new UnauthorizedAccessException("Access to user from different tenant is not allowed.");

            return user;
        }

        public async Task<IReadOnlyList<DemoUser>> ListUsersAsync(TenantId tenantId, CancellationToken ct = default)
            => await dbContext.Users
                .Where(u => u.TenantId == tenantId)
                .OrderBy(u => u.DisplayName)
                .ToListAsync(ct);

        public async Task UpdateProfileAsync(Guid userId, TenantId tenantId, string displayName, CancellationToken ct = default)
        {
            var user = await GetUserByIdAsync(userId, tenantId, ct)
                ?? throw new KeyNotFoundException($"User {userId} not found.");

            user.UpdateProfile(displayName);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("User profile updated: {UserId}", userId);
        }

        public async Task ChangePasswordAsync(Guid userId, TenantId tenantId, string plainPassword, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(plainPassword))
                throw new ArgumentException("Password cannot be empty.", nameof(plainPassword));

            var user = await GetUserByIdAsync(userId, tenantId, ct)
                ?? throw new KeyNotFoundException($"User {userId} not found.");

            string passwordHash;
            try
            {
                passwordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword, 12);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to hash password. Please try again.", ex);
            }

            user.ChangePassword(passwordHash);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("User password changed: {UserId}", userId);
        }

        public async Task DeactivateUserAsync(Guid userId, TenantId tenantId, CancellationToken ct = default)
        {
            var user = await GetUserByIdAsync(userId, tenantId, ct)
                ?? throw new KeyNotFoundException($"User {userId} not found.");

            if (user.Role == UserRole.Owner)
            {
                var activeOwners = await dbContext.Users
                    .CountAsync(u => u.TenantId == tenantId && u.Role == UserRole.Owner && u.IsActive, ct);

                if (activeOwners <= 1)
                    throw new InvalidOperationException("Cannot deactivate the last Owner of a tenant.");
            }

            user.Deactivate();
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("User deactivated: {UserId}", userId);
        }

        public async Task ReactivateUserAsync(Guid userId, TenantId tenantId, CancellationToken ct = default)
        {
            var user = await GetUserByIdAsync(userId, tenantId, ct)
                ?? throw new KeyNotFoundException($"User {userId} not found.");

            user.Reactivate();
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("User reactivated: {UserId}", userId);
        }

        // ── Private event handlers ─────────────────────────────────────────────

        private async Task HandleUserCreatedAsync(UserCreatedEvent evt)
        {
            try
            {
                var subject = "Chào mừng bạn đến với Vạn An ShopERP";
                var body = WelcomeEmailBody(evt.Username, evt.DisplayName);
                await notificationService.SendEmailAsync(evt.Username, subject, body);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Welcome email failed for user {UserId}", evt.UserId);
            }
        }

        private static string WelcomeEmailBody(string username, string displayName)
            => $"""
                <html>
                <body>
                  <h2>Chào mừng {displayName} đến với Vạn An ShopERP!</h2>
                  <p>Tài khoản của bạn đã được tạo thành công.</p>
                  <p>Email đăng nhập: <strong>{username}</strong></p>
                  <p>Vui lòng đổi mật khẩu sau lần đăng nhập đầu tiên.</p>
                </body>
                </html>
                """;
    }
}
