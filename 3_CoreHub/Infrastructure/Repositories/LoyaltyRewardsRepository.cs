using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VanAn.CoreHub.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Repositories
{
    public class LoyaltyRewardsRepository(IVanAnDbContext context) : ILoyaltyRewardsRepository
    {
        private readonly IVanAnDbContext _context = context;

        public async Task<LoyaltyRewards?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            // Loyalty Points Integrity (Batch 2): prefer the row of the CURRENT tenant context
            // (query filter applies TenantId == ambient tenant). Since LoyaltyRewards is now
            // per-(customer, tenant), reading the first row across tenants can surface the wrong
            // tenant's balance. Fall back to IgnoreQueryFilters (first row) only when the ambient
            // tenant has no row (e.g. SystemAdmin/background scope with TenantId = Empty).
            LoyaltyRewards? scoped = await _context.LoyaltyRewards
                .FirstOrDefaultAsync(r => r.CustomerId == customerId, cancellationToken);
            if (scoped is not null)
            {
                return scoped;
            }

            return await _context.LoyaltyRewards
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.CustomerId == customerId, cancellationToken);
        }

        public async Task<LoyaltyRewards?> GetByCustomerAndTenantIdAsync(Guid customerId, TenantId tenantId, CancellationToken cancellationToken = default)
        {
            // Explicit cross-tenant lookup: (CustomerId, TenantId) unique index (LoyaltyRewardsConfiguration).
            return await _context.LoyaltyRewards
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.CustomerId == customerId && r.TenantId == tenantId, cancellationToken);
        }

        public async Task<Customer?> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            // Bug 6 fix: IgnoreQueryFilters — customer ID is globally unique (PK).
            // Without this, the global TenantId query filter excludes the customer stub
            // when ITenantProvider.TenantId doesn't match (e.g., SystemAdmin impersonation
            // context, or customer created in a different tenant scope).
            return await _context.Customers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        }

        public async Task<IEnumerable<LoyaltyRewards>> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken = default)
        {
            return await _context.LoyaltyRewards
                .Where(r => r.TenantId == tenantId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<LoyaltyRewards>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            return await _context.LoyaltyRewards
                .Where(r => r.IsActive)
                .ToListAsync(cancellationToken);
        }

        public async Task<LoyaltyRewards> AddAsync(LoyaltyRewards reward, CancellationToken cancellationToken = default)
        {
            _ = await _context.LoyaltyRewards.AddAsync(reward, cancellationToken);
            return reward;
        }

        public async Task<LoyaltyRewards> UpdateAsync(LoyaltyRewards reward, CancellationToken cancellationToken = default)
        {
            _ = _context.LoyaltyRewards.Update(reward);
            return reward;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _ = await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return await _context.BeginTransactionAsync(cancellationToken);
        }

        public async Task<int> GetTotalPointsAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            LoyaltyRewards? rewards = await GetByCustomerIdAsync(customerId, cancellationToken);
            return rewards?.PointBalance ?? 0;
        }
    }
}
