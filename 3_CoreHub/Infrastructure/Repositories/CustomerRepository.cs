using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of ICustomerRepository
/// Engineering Constitution Compliance: ALWAYS filter by tenant and soft delete
/// </summary>
public class CustomerRepository : ICustomerRepository
{
    private readonly VanAnDbContext _context;
    private readonly Guid _currentTenantId;

    public CustomerRepository(VanAnDbContext context)
    {
        _context = context;
        _currentTenantId = context.CurrentTenantId;
    }

    public async Task<Customer?> GetByIdAsync(Guid id)
    {
        return await _context.Customers
            .Where(c => c.Id == id && 
                       c.TenantId == _currentTenantId && 
                       !c.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<Customer?> GetByDeviceIdAsync(Guid deviceId)
    {
        return await _context.Customers
            .Where(c => c.DeviceId == deviceId && 
                       c.TenantId == _currentTenantId && 
                       !c.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<Customer>> GetAllActiveAsync()
    {
        return await _context.Customers
            .Where(c => c.TenantId == _currentTenantId && 
                       !c.IsDeleted)
            .OrderBy(c => c.FullName)
            .ToListAsync();
    }

    public async Task<Customer> AddAsync(Customer customer)
    {
        // Ensure tenant compliance
        customer.TenantId = _currentTenantId;
        customer.CreatedAt = DateTime.UtcNow;
        customer.IsDeleted = false;

        await _context.Customers.AddAsync(customer);
        await _context.SaveChangesAsync();

        return customer;
    }

    public async Task<Customer> UpdateAsync(Customer customer)
    {
        // Security: Verify customer belongs to current tenant and is not deleted
        var existingCustomer = await GetByIdAsync(customer.Id);
        if (existingCustomer == null)
        {
            throw new InvalidOperationException("Customer not found or access denied");
        }

        // Update audit fields
        customer.UpdatedAt = DateTime.UtcNow;
        customer.TenantId = _currentTenantId;

        _context.Customers.Update(customer);
        await _context.SaveChangesAsync();

        return customer;
    }

    public async Task<bool> SoftDeleteAsync(Guid id)
    {
        var customer = await GetByIdAsync(id);
        if (customer == null)
        {
            return false;
        }

        customer.IsDeleted = true;
        customer.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsByDeviceIdAsync(Guid deviceId)
    {
        return await _context.Customers
            .AnyAsync(c => c.DeviceId == deviceId && 
                         c.TenantId == _currentTenantId && 
                         !c.IsDeleted);
    }

    public async Task<Customer?> GetWithOrdersAsync(Guid id)
    {
        return await _context.Customers
            .Include(c => c.Orders)
            .Where(c => c.Id == id && 
                       c.TenantId == _currentTenantId && 
                       !c.IsDeleted)
            .FirstOrDefaultAsync();
    }
}
