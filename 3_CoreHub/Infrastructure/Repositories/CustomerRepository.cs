using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of ICustomerRepository
/// Engineering Constitution Compliance: ALWAYS filter by tenant and soft delete
/// Decoupled from VanAnDbContext using IVanAnDbContext for Offline-First architecture
/// </summary>
public class CustomerRepository : ICustomerRepository
{
    private readonly IVanAnDbContext _context;
    private readonly Guid _currentTenantId;

    public CustomerRepository(IVanAnDbContext context)
    {
        _context = context;
        _currentTenantId = context is VanAnDbContext vanAnContext ? vanAnContext.CurrentTenantId : Guid.Empty;
    }

    public async Task<Customer?> GetByIdAsync(Guid id)
    {
        var tenantId = _currentTenantId;
        return await _context.Customers
            .Where(c => c.Id == id && 
                       c.TenantId == new TenantId(tenantId) && 
                       !c.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<Customer?> GetByDeviceIdAsync(Guid deviceId)
    {
        var tenantId = _currentTenantId;
        return await _context.Customers
            .Where(c => c.DeviceId == deviceId && 
                       c.TenantId == new TenantId(tenantId) && 
                       !c.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<Customer>> GetAllActiveAsync()
    {
        var tenantId = _currentTenantId;
        return await _context.Customers
            .Where(c => c.TenantId == new TenantId(tenantId) && 
                       !c.IsDeleted)
            .OrderBy(c => c.FullName)
            .ToListAsync();
    }

    public async Task<Customer> AddAsync(Customer customer)
    {
        // Create new customer with proper constructor
        var newCustomer = new Customer(new TenantId(_currentTenantId), customer.FullName, customer.PhoneNumber, customer.Email);
        
        // Copy other properties if needed
        newCustomer.UpdateCustomerDetails(customer.FullName, customer.PhoneNumber, customer.Email, customer.CustomerTier, customer.DeviceId, customer.IsActive);

        await _context.Customers.AddAsync(newCustomer);
        await _context.SaveChangesAsync();

        return newCustomer;
    }

    public async Task<Customer> UpdateAsync(Customer customer)
    {
        // Security: Verify customer belongs to current tenant and is not deleted
        var existingCustomer = await GetByIdAsync(customer.Id);
        if (existingCustomer == null)
        {
            throw new InvalidOperationException("Customer not found or access denied");
        }

        // Update existing customer properties
        existingCustomer.UpdateCustomerDetails(customer.FullName, customer.PhoneNumber, customer.Email, customer.CustomerTier, customer.DeviceId, customer.IsActive);

        await _context.SaveChangesAsync();

        return existingCustomer;
    }

    public async Task<bool> SoftDeleteAsync(Guid id)
    {
        var customer = await GetByIdAsync(id);
        if (customer == null)
        {
            return false;
        }

        customer.SoftDelete();

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsByDeviceIdAsync(Guid deviceId)
    {
        var tenantId = _currentTenantId;
        return await _context.Customers
            .AnyAsync(c => c.DeviceId == deviceId && 
                         c.TenantId == new TenantId(tenantId) && 
                         !c.IsDeleted);
    }

    public async Task<Customer?> GetWithOrdersAsync(Guid id)
    {
        var tenantId = _currentTenantId;
        return await _context.Customers
            .Include(c => c.Orders)
            .Where(c => c.Id == id && 
                       c.TenantId == new TenantId(tenantId) && 
                       !c.IsDeleted)
            .FirstOrDefaultAsync();
    }
}
