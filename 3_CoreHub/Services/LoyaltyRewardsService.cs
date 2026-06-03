using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Repositories;
using System.Text.Json;

namespace VanAn.CoreHub.Services
{
    public class LoyaltyRewardsService(ILoyaltyRewardsRepository repository, ILogger<LoyaltyRewardsService> logger) : ILoyaltyRewardsService
    {
        private readonly ILoyaltyRewardsRepository _repository = repository;
        private readonly ILogger<LoyaltyRewardsService> _logger = logger;

        public async Task<LoyaltyRewards> GetOrCreateCustomerRewardsAsync(Guid customerId)
        {
            LoyaltyRewards? rewards = await _repository.GetByCustomerIdAsync(customerId);

            if (rewards == null)
            {
                TenantId tenantId = new(Guid.NewGuid()); // Will be set by repository
                rewards = new LoyaltyRewards(tenantId, customerId);
                rewards.UpdateHistory(JsonSerializer.Serialize(new List<LoyaltyHistoryEntry>()));

                await _repository.AddAsync(rewards);
                await _repository.SaveChangesAsync();

                _logger.LogInformation("Created new loyalty rewards for customer {CustomerId}", customerId);
            }

            return rewards;
        }

        public async Task<bool> AddPointsAsync(Guid customerId, int points, string reason)
        {
            if (points <= 0)
            {
                return false;
            }

            using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await _repository.BeginTransactionAsync();
            try
            {
                LoyaltyRewards rewards = await GetOrCreateCustomerRewardsAsync(customerId);

                rewards.AddPoints(points, reason);

                // Add history entry
                List<LoyaltyHistoryEntry> history = GetHistoryEntries(rewards.History);
                history.Add(new LoyaltyHistoryEntry
                {
                    Type = "EARN",
                    Points = points,
                    Reason = reason,
                    Timestamp = DateTime.UtcNow,
                    BalanceAfter = rewards.PointBalance
                });
                rewards.UpdateHistory(JsonSerializer.Serialize(history));

                await _repository.UpdateAsync(rewards);
                await _repository.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Added {Points} points to customer {CustomerId}. New balance: {Balance}",
                    points, customerId, rewards.PointBalance);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to add points to customer {CustomerId}", customerId);
                return false;
            }
        }

        public async Task<bool> SubtractPointsAsync(Guid customerId, int points, string reason)
        {
            if (points <= 0)
            {
                return false;
            }

            using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await _repository.BeginTransactionAsync();
            try
            {
                LoyaltyRewards rewards = await GetOrCreateCustomerRewardsAsync(customerId);

                if (rewards.PointBalance < points)
                {
                    _logger.LogWarning("Insufficient points for customer {CustomerId}. Available: {Balance}, Requested: {Points}",
                        customerId, rewards.PointBalance, points);
                    return false;
                }

                rewards.DeductPoints(points, reason);

                // Add history entry
                List<LoyaltyHistoryEntry> history = GetHistoryEntries(rewards.History);
                history.Add(new LoyaltyHistoryEntry
                {
                    Type = "SPEND",
                    Points = -points,
                    Reason = reason,
                    Timestamp = DateTime.UtcNow,
                    BalanceAfter = rewards.PointBalance
                });
                rewards.UpdateHistory(JsonSerializer.Serialize(history));

                await _repository.UpdateAsync(rewards);
                await _repository.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Subtracted {Points} points from customer {CustomerId}. New balance: {Balance}",
                    points, customerId, rewards.PointBalance);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to subtract points from customer {CustomerId}", customerId);
                return false;
            }
        }

        public async Task<LoyaltyRewards?> GetCustomerRewardsAsync(Guid customerId)
        {
            return await _repository.GetByCustomerIdAsync(customerId);
        }

        public async Task<List<LoyaltyRewards>> GetAllRewardsAsync()
        {
            IEnumerable<LoyaltyRewards> rewards = await _repository.GetActiveAsync();
            return rewards.ToList();
        }

        public async Task<bool> UpdateHistoryAsync(Guid customerId, string historyEntry)
        {
            LoyaltyRewards? rewards = await GetCustomerRewardsAsync(customerId);
            if (rewards == null)
            {
                return false;
            }

            rewards.UpdateHistory(historyEntry);

            await _repository.UpdateAsync(rewards);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Updated history for customer {CustomerId}", customerId);
            return true;
        }

        private static List<LoyaltyHistoryEntry> GetHistoryEntries(string historyJson)
        {
            try
            {
                return JsonSerializer.Deserialize<List<LoyaltyHistoryEntry>>(historyJson) ?? [];
            }
            catch
            {
                return [];
            }
        }

        public async Task<bool> ActivateCustomerAsync(Guid customerId)
        {
            try
            {
                _logger.LogInformation("Activating loyalty program for customer {CustomerId}", customerId);

                // Get or create customer rewards
                LoyaltyRewards rewards = await GetOrCreateCustomerRewardsAsync(customerId);

                // Add welcome bonus points
                await AddPointsAsync(customerId, 100, "Welcome bonus for joining loyalty program");

                _logger.LogInformation("Loyalty program activated for customer {CustomerId}", customerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to activate loyalty program for customer {CustomerId}", customerId);
                return false;
            }
        }
    }

    // Helper class for loyalty history entries
    public class LoyaltyHistoryEntry
    {
        public string Type { get; set; } = string.Empty; // EARN, SPEND
        public int Points { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int BalanceAfter { get; set; }
    }
}
