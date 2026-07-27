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
            // Bug 6 fix: IgnoreQueryFilters — loyalty rewards lookup by CustomerId should
            // not be restricted by tenant filter (CustomerId is globally unique PK).
            return await _context.LoyaltyRewards
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.CustomerId == customerId, cancellationToken);
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
