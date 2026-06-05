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
            Guid tenantId = _currentTenantId;
            return await _context.Customers
                .Where(c => c.Id == id &&
                           c.TenantId == new TenantId(tenantId) &&
                           !c.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<Customer?> GetByDeviceIdAsync(Guid deviceId)
        {
            Guid tenantId = _currentTenantId;
            return await _context.Customers
                .Where(c => c.DeviceId == deviceId &&
                           c.TenantId == new TenantId(tenantId) &&
                           !c.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<Customer>> GetAllActiveAsync()
        {
            Guid tenantId = _currentTenantId;
            return await _context.Customers
                .Where(c => c.TenantId == new TenantId(tenantId) &&
                           !c.IsDeleted)
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
            Guid tenantId = _currentTenantId;
            return await _context.Customers
                .AnyAsync(c => c.DeviceId == deviceId &&
                             c.TenantId == new TenantId(tenantId) &&
                             !c.IsDeleted);
        }

        public async Task<Customer?> GetWithOrdersAsync(Guid id)
        {
            Guid tenantId = _currentTenantId;
            return await _context.Customers
                .Include(c => c.Orders)
                .Where(c => c.Id == id &&
                           c.TenantId == new TenantId(tenantId) &&
                           !c.IsDeleted)
                .FirstOrDefaultAsync();
        }
    }
}
