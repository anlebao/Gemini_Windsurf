using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

public interface ILoyaltyRewardsService
{
    Task<LoyaltyRewards> GetOrCreateCustomerRewardsAsync(Guid customerId);
    Task<bool> AddPointsAsync(Guid customerId, int points, string reason);
    Task<bool> SubtractPointsAsync(Guid customerId, int points, string reason);
    Task<LoyaltyRewards?> GetCustomerRewardsAsync(Guid customerId);
    Task<List<LoyaltyRewards>> GetAllRewardsAsync();
    Task<bool> UpdateHistoryAsync(Guid customerId, string historyEntry);
    Task<bool> ActivateCustomerAsync(Guid customerId);
}
