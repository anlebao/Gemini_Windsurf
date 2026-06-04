using Microsoft.EntityFrameworkCore;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Services
{
    public interface IShopService
    {
        Task<Shop> CreateShopAsync(Shop shop);
        Task<Shop?> GetShopByIdAsync(Guid shopId);
        Task<List<Shop>> GetShopsAsync();
        Task<Shop> UpdateShopAsync(Shop shop);
        Task<bool> DeleteShopAsync(Guid shopId);
    }

    public class ShopService(VanAnDbContext context, ITenantProvider tenantProvider) : IShopService
    {
        private readonly VanAnDbContext _context = context;
        private readonly ITenantProvider _tenantProvider = tenantProvider;

        public async Task<Shop> CreateShopAsync(Shop shop)
        {
            // Create new Shop with proper constructor
            Shop newShop = new(_tenantProvider.TenantId, shop.Name, shop.Address, shop.Phone, shop.Email);

            _ = _context.Shops.Add(newShop);
            _ = await _context.SaveChangesAsync();

            return newShop;
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
            Shop? existing = await GetShopByIdAsync(shop.Id) ?? throw new InvalidOperationException("Shop not found or access denied");
            existing.UpdateShopDetails(shop.Name, shop.Address, shop.Phone, shop.Email, shop.IsActive);

            _ = await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteShopAsync(Guid shopId)
        {
            Shop? shop = await GetShopByIdAsync(shopId);
            if (shop == null)
            {
                return false;
            }

            _ = _context.Shops.Remove(shop);
            _ = await _context.SaveChangesAsync();
            return true;
        }
    }
}
