using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services
{
    public interface ILoyaltyRewardsService
    {
        Task<LoyaltyRewards> CreateRewardsAsync(LoyaltyRewards rewards);
        Task<LoyaltyRewards?> GetRewardsByCustomerIdAsync(Guid customerId);
        Task<List<LoyaltyRewards>> GetRewardsByShopAsync(Guid shopId);
        Task<bool> AddPointsAsync(Guid customerId, decimal points, string reason);
        Task<bool> RedeemPointsAsync(Guid customerId, decimal points, string reward);
        Task<List<LoyaltyRewards>> GetHistoryAsync(Guid customerId);
        Task<decimal> GetAvailablePointsAsync(Guid customerId);
    }
}
