using Microsoft.EntityFrameworkCore;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Services;

public interface IShopService
{
    Task<Shop> CreateShopAsync(Shop shop);
    Task<Shop?> GetShopByIdAsync(Guid shopId);
    Task<List<Shop>> GetShopsAsync();
    Task<Shop> UpdateShopAsync(Shop shop);
    Task<bool> DeleteShopAsync(Guid shopId);
}

public class ShopService : IShopService
{
    private readonly VanAnDbContext _context;
    private readonly ITenantProvider _tenantProvider;

    public ShopService(VanAnDbContext context, ITenantProvider tenantProvider)
    {
        _context = context;
        _tenantProvider = tenantProvider;
    }

    public async Task<Shop> CreateShopAsync(Shop shop)
    {
        // Auto-set TenantId từ provider
        shop.TenantId = _tenantProvider.TenantId;
        
        _context.Shops.Add(shop);
        await _context.SaveChangesAsync();
        
        return shop;
    }

    public async Task<Shop?> GetShopByIdAsync(Guid shopId)
    {
        // Global query filter sẽ tự động áp dụng TenantId
        return await _context.Shops
            .FirstOrDefaultAsync(s => s.Id == shopId);
    }

    public async Task<List<Shop>> GetShopsAsync()
    {
        // Global query filter sẽ tự động lọc theo TenantId
        return await _context.Shops
            .ToListAsync();
    }

    public async Task<Shop> UpdateShopAsync(Shop shop)
    {
        // Verify tenant ownership
        var existing = await GetShopByIdAsync(shop.Id);
        if (existing == null)
            throw new InvalidOperationException("Shop not found or access denied");

        existing.Name = shop.Name;
        existing.Address = shop.Address;
        existing.Phone = shop.Phone;
        existing.Email = shop.Email;
        existing.IsActive = shop.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteShopAsync(Guid shopId)
    {
        var shop = await GetShopByIdAsync(shopId);
        if (shop == null)
            return false;

        _context.Shops.Remove(shop);
        await _context.SaveChangesAsync();
        return true;
    }
}
