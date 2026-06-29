using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Repositories
{
    /// <summary>
    /// Wave 9: EF Core implementation of IPushSubscriptionRepository
    /// Engineering Constitution Compliance: ALWAYS filter by tenant and soft delete
    /// Decoupled from VanAnDbContext using IVanAnDbContext for Offline-First architecture
    /// </summary>
    public class PushSubscriptionRepository(IVanAnDbContext context) : IPushSubscriptionRepository
    {
        private readonly IVanAnDbContext _context = context;
        private readonly Guid _currentTenantId = context is VanAnDbContext vanAnContext ? vanAnContext.CurrentTenantId : Guid.Empty;

        public async Task<PushSubscription?> GetByIdAsync(Guid id)
        {
            return await _context.PushSubscriptions
                .Where(s => s.Id == id && !s.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<PushSubscription>> GetByCustomerIdAsync(Guid customerId)
        {
            return await _context.PushSubscriptions
                .Where(s => s.CustomerId == customerId && !s.IsDeleted && s.IsActive)
                .OrderByDescending(s => s.LastUsedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<PushSubscription>> GetAllActiveAsync()
        {
            return await _context.PushSubscriptions
                .Where(s => !s.IsDeleted && s.IsActive)
                .OrderByDescending(s => s.LastUsedAt)
                .ToListAsync();
        }

        public async Task<PushSubscription> AddAsync(PushSubscription subscription)
        {
            // Create new subscription with proper constructor
            PushSubscription newSubscription = new(
                new TenantId(_currentTenantId),
                subscription.CustomerId,
                subscription.SubscriptionJson,
                subscription.UserAgent);

            _ = await _context.PushSubscriptions.AddAsync(newSubscription);
            _ = await _context.SaveChangesAsync();

            return newSubscription;
        }

        public async Task<PushSubscription> UpdateAsync(PushSubscription subscription)
        {
            var existing = await GetByIdAsync(subscription.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"PushSubscription with ID {subscription.Id} not found");
            }

            existing.UpdateSubscription(subscription.SubscriptionJson, subscription.UserAgent);
            _ = await _context.SaveChangesAsync();

            return existing;
        }

        public async Task<bool> SoftDeleteAsync(Guid id)
        {
            var subscription = await GetByIdAsync(id);
            if (subscription == null)
            {
                return false;
            }

            subscription.MarkAsInactive();
            _ = await _context.SaveChangesAsync();

            return true;
        }

        public async Task<int> DeleteExpiredAsync()
        {
            var expiredSubscriptions = await _context.PushSubscriptions
                .Where(s => !s.IsDeleted && s.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            foreach (var subscription in expiredSubscriptions)
            {
                subscription.MarkAsInactive();
            }

            return await _context.SaveChangesAsync();
        }

        public async Task<PushSubscription> GetOrCreateAsync(Guid customerId, string subscriptionJson, string? userAgent = null)
        {
            // Try to find existing active subscription
            var existing = await _context.PushSubscriptions
                .Where(s => s.CustomerId == customerId && !s.IsDeleted && s.IsActive)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                // Update existing subscription
                existing.UpdateSubscription(subscriptionJson, userAgent);
                _ = await _context.SaveChangesAsync();
                return existing;
            }

            // Create new subscription
            return await AddAsync(new PushSubscription(
                new TenantId(_currentTenantId),
                customerId,
                subscriptionJson,
                userAgent));
        }
    }
}