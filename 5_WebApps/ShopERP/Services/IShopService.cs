using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services
{
    public interface IShopService
    {
        Task<Shop> CreateShopAsync(Shop shop);
        Task<Shop?> GetShopByIdAsync(Guid shopId);
        Task<List<Shop>> GetShopsByTenantAsync(Guid tenantId);
        Task<Shop> UpdateShopAsync(Shop shop);
        Task<bool> DeleteShopAsync(Guid shopId);
        Task<List<Shop>> GetAllShopsAsync();
    }
}
