using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using System.Text.Json;

namespace VanAn.CoreHub.Services;

public class LoyaltyRewardsService : ILoyaltyRewardsService
{
    private readonly VanAnDbContext _context;
    private readonly ILogger<LoyaltyRewardsService> _logger;

    public LoyaltyRewardsService(VanAnDbContext context, ILogger<LoyaltyRewardsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LoyaltyRewards> GetOrCreateCustomerRewardsAsync(Guid customerId)
    {
        var rewards = await _context.LoyaltyRewards
            .FirstOrDefaultAsync(r => r.CustomerId == customerId);

        if (rewards == null)
        {
            rewards = new LoyaltyRewards
            {
                CustomerId = customerId,
                PointBalance = 0,
                History = JsonSerializer.Serialize(new List<LoyaltyHistoryEntry>()),
                IsActive = true
            };
            
            await _context.LoyaltyRewards.AddAsync(rewards);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Created new loyalty rewards for customer {CustomerId}", customerId);
        }

        return rewards;
    }

    public async Task<bool> AddPointsAsync(Guid customerId, int points, string reason)
    {
        if (points <= 0)
            return false;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var rewards = await GetOrCreateCustomerRewardsAsync(customerId);
            
            rewards.PointBalance += points;
            rewards.UpdatedAt = DateTime.UtcNow;
            
            // Add history entry
            var history = GetHistoryEntries(rewards.History);
            history.Add(new LoyaltyHistoryEntry
            {
                Type = "EARN",
                Points = points,
                Reason = reason,
                Timestamp = DateTime.UtcNow,
                BalanceAfter = rewards.PointBalance
            });
            rewards.History = JsonSerializer.Serialize(history);

            await _context.SaveChangesAsync();
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
            return false;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var rewards = await GetOrCreateCustomerRewardsAsync(customerId);
            
            if (rewards.PointBalance < points)
            {
                _logger.LogWarning("Insufficient points for customer {CustomerId}. Available: {Balance}, Requested: {Points}", 
                    customerId, rewards.PointBalance, points);
                return false;
            }

            rewards.PointBalance -= points;
            rewards.UpdatedAt = DateTime.UtcNow;
            
            // Add history entry
            var history = GetHistoryEntries(rewards.History);
            history.Add(new LoyaltyHistoryEntry
            {
                Type = "SPEND",
                Points = -points,
                Reason = reason,
                Timestamp = DateTime.UtcNow,
                BalanceAfter = rewards.PointBalance
            });
            rewards.History = JsonSerializer.Serialize(history);

            await _context.SaveChangesAsync();
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
        return await _context.LoyaltyRewards
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.CustomerId == customerId && r.IsActive);
    }

    public async Task<List<LoyaltyRewards>> GetAllRewardsAsync()
    {
        return await _context.LoyaltyRewards
            .Include(r => r.Customer)
            .Where(r => r.IsActive)
            .ToListAsync();
    }

    public async Task<bool> UpdateHistoryAsync(Guid customerId, string historyEntry)
    {
        var rewards = await GetCustomerRewardsAsync(customerId);
        if (rewards == null)
            return false;

        rewards.History = historyEntry;
        rewards.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Updated history for customer {CustomerId}", customerId);
        return true;
    }

    private List<LoyaltyHistoryEntry> GetHistoryEntries(string historyJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<LoyaltyHistoryEntry>>(historyJson) ?? new List<LoyaltyHistoryEntry>();
        }
        catch
        {
            return new List<LoyaltyHistoryEntry>();
        }
    }

    public async Task<bool> ActivateCustomerAsync(Guid customerId)
    {
        try
        {
            _logger.LogInformation("Activating loyalty program for customer {CustomerId}", customerId);
            
            // Get or create customer rewards
            var rewards = await GetOrCreateCustomerRewardsAsync(customerId);
            
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
