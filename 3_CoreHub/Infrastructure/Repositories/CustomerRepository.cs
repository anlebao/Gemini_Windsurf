using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Repositories
{
    /// <summary>
    /// EF Core implementation of ICustomerRepository
    /// Engineering Constitution Compliance: ALWAYS filter by tenant and soft delete
    /// Decoupled from VanAnDbContext using IVanAnDbContext for Offline-First architecture
    /// </summary>
    public class CustomerRepository(IVanAnDbContext context) : ICustomerRepository
    {
        private readonly IVanAnDbContext _context = context;
        private readonly Guid _currentTenantId = context is VanAnDbContext vanAnContext ? vanAnContext.CurrentTenantId : Guid.Empty;

        public async Task<Customer?> GetByIdAsync(Guid id)
        {
            return await _context.Customers
                .Where(c => c.Id == id && !c.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<Customer?> GetByDeviceIdAsync(Guid deviceId)
        {
            return await _context.Customers
                .Where(c => c.DeviceId == deviceId && !c.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<Customer>> GetAllActiveAsync()
        {
            return await _context.Customers
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.FullName)
                .ToListAsync();
        }

        public async Task<Customer> AddAsync(Customer customer)
        {
            // Create new customer with proper constructor
            Customer newCustomer = new(new TenantId(_currentTenantId), customer.FullName, customer.PhoneNumber, customer.Email);

            // Copy other properties if needed
            newCustomer.UpdateCustomerDetails(customer.FullName, customer.PhoneNumber, customer.Email, customer.CustomerTier, customer.DeviceId, customer.IsActive);

            _ = await _context.Customers.AddAsync(newCustomer);
            _ = await _context.SaveChangesAsync();

            return newCustomer;
        }

        public async Task<Customer> UpdateAsync(Customer customer)
        {
            // Security: Verify customer belongs to current tenant and is not deleted
            Customer? existingCustomer = await GetByIdAsync(customer.Id) ?? throw new InvalidOperationException("Customer not found or access denied");

            // Update existing customer properties
            existingCustomer.UpdateCustomerDetails(customer.FullName, customer.PhoneNumber, customer.Email, customer.CustomerTier, customer.DeviceId, customer.IsActive);

            _ = await _context.SaveChangesAsync();

            return existingCustomer;
        }

        public async Task<bool> SoftDeleteAsync(Guid id)
        {
            Customer? customer = await GetByIdAsync(id);
            if (customer == null)
            {
                return false;
            }

            customer.SoftDelete();

            _ = await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsByDeviceIdAsync(Guid deviceId)
        {
            return await _context.Customers
                .AnyAsync(c => c.DeviceId == deviceId && !c.IsDeleted);
        }

        public async Task<Customer?> GetWithOrdersAsync(Guid id)
        {
            return await _context.Customers
                .Include(c => c.Orders)
                .Where(c => c.Id == id && !c.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<Customer?> GetByPhoneAsync(string phoneNumber)
        {
            return await _context.Customers
                .Where(c => c.PhoneNumber == phoneNumber && !c.IsDeleted)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Phase 5 + WS-2: Get customers matching segmentation criteria for bulk push campaigns + CRM list.
        /// WS-2 additions: MinPointBalance/MaxPointBalance (join LoyaltyRewards), BirthdayMonth, LastOrderWithinDays.
        /// </summary>
        public async Task<IReadOnlyList<Customer>> GetBySegmentAsync(CustomerSegmentCriteria criteria)
        {
            IQueryable<Customer> query = _context.Customers
                .Where(c => !c.IsDeleted && c.IsActive);

            if (!string.IsNullOrEmpty(criteria.CustomerTier))
                query = query.Where(c => c.CustomerTier == criteria.CustomerTier);

            if (criteria.MinIdentityLevel.HasValue)
                query = query.Where(c => c.IdentityLevel >= criteria.MinIdentityLevel.Value);

            if (criteria.MinTotalSpent.HasValue)
                query = query.Where(c => c.TotalSpent >= criteria.MinTotalSpent.Value);

            if (criteria.MaxTotalSpent.HasValue)
                query = query.Where(c => c.TotalSpent <= criteria.MaxTotalSpent.Value);

            // WS-2: LastOrderWithinDays convenience filter (convert to LastOrderAfter)
            DateTime? lastOrderAfter = criteria.LastOrderAfter;
            if (criteria.LastOrderWithinDays.HasValue && criteria.LastOrderWithinDays.Value > 0)
            {
                DateTime computed = DateTime.UtcNow.AddDays(-criteria.LastOrderWithinDays.Value);
                lastOrderAfter = lastOrderAfter.HasValue && lastOrderAfter.Value > computed ? lastOrderAfter.Value : computed;
            }

            if (lastOrderAfter.HasValue)
                query = query.Where(c => c.LastOrderDate >= lastOrderAfter.Value);

            if (criteria.LastOrderBefore.HasValue)
                query = query.Where(c => c.LastOrderDate <= criteria.LastOrderBefore.Value);

            // WS-2: Birthday month filter (1-12)
            if (criteria.BirthdayMonth.HasValue && criteria.BirthdayMonth.Value >= 1 && criteria.BirthdayMonth.Value <= 12)
                query = query.Where(c => c.Birthday != null && c.Birthday.Value.Month == criteria.BirthdayMonth.Value);

            if (criteria.HasPushSubscription)
            {
                // Join with PushSubscriptions to find customers with active push subscriptions
                var customerIdsWithPush = _context.PushSubscriptions
                    .Where(ps => ps.IsActive && !ps.IsDeleted)
                    .Select(ps => ps.CustomerId)
                    .Distinct();

                query = query.Where(c => customerIdsWithPush.Contains(c.Id));
            }

            // WS-2: Loyalty points range filter (join LoyaltyRewards table)
            if (criteria.MinPointBalance.HasValue || criteria.MaxPointBalance.HasValue)
            {
                var rewardsQuery = _context.LoyaltyRewards.AsQueryable();
                if (criteria.MinPointBalance.HasValue)
                    rewardsQuery = rewardsQuery.Where(r => r.PointBalance >= criteria.MinPointBalance.Value);
                if (criteria.MaxPointBalance.HasValue)
                    rewardsQuery = rewardsQuery.Where(r => r.PointBalance <= criteria.MaxPointBalance.Value);

                var customerIdsWithPoints = rewardsQuery
                    .Select(r => r.CustomerId)
                    .Distinct();

                query = query.Where(c => customerIdsWithPoints.Contains(c.Id));
            }

            return await query
                .OrderBy(c => c.FullName)
                .ToListAsync();
        }

        /// <summary>
        /// Loyalty-C WS-B: Get all active customers whose birthday (month + day) matches today's UTC date.
        /// Birthday is stored as date-only; comparison is on Month + Day only (year ignored for annual recurrence).
        /// </summary>
        public async Task<IReadOnlyList<Customer>> GetCustomersWithBirthdayTodayAsync()
        {
            DateTime todayUtc = DateTime.UtcNow.Date;
            int month = todayUtc.Month;
            int day = todayUtc.Day;

            return await _context.Customers
                .Where(c => !c.IsDeleted && c.IsActive && c.Birthday != null && c.Birthday.Value.Month == month && c.Birthday.Value.Day == day)
                .OrderBy(c => c.FullName)
                .ToListAsync();
        }
    }
}
