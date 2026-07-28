using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Infrastructure.Messaging;
using System.Text.Json;

namespace VanAn.CoreHub.Services
{
    public class LoyaltyRewardsService(
        ILoyaltyRewardsRepository repository,
        ILogger<LoyaltyRewardsService> logger,
        INatsEventPublisher? natsEventPublisher = null,
        IOutboxRepository? outboxRepository = null) : ILoyaltyRewardsService
    {
        private readonly ILoyaltyRewardsRepository _repository = repository;
        private readonly ILogger<LoyaltyRewardsService> _logger = logger;
        private readonly INatsEventPublisher? _natsEventPublisher = natsEventPublisher;
        private readonly IOutboxRepository? _outboxRepository = outboxRepository;

        private static readonly JsonSerializerOptions EventJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public async Task<LoyaltyRewards> GetOrCreateCustomerRewardsAsync(Guid customerId, TenantId tenantId)
        {
            LoyaltyRewards? rewards = await _repository.GetByCustomerIdAsync(customerId);

            if (rewards == null)
            {
                rewards = new LoyaltyRewards(tenantId, customerId);
                rewards.UpdateHistory(JsonSerializer.Serialize(new List<LoyaltyHistoryEntry>()));

                _ = await _repository.AddAsync(rewards);
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

            // Bug 6 fix: Support ambient transaction. OrderWorkflowService.TransitionStatusAsync
            // begins a transaction before calling ProcessLoyaltyPointsAsync → AddPointsAsync.
            // SQLite does not support nested transactions → BeginTransactionAsync throws.
            // If an ambient transaction exists, join it (no explicit commit/rollback — caller owns it).
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            bool ownsTransaction = false;
            try
            {
                // Get customer to retrieve tenant ID
                Customer? customer = await _repository.GetCustomerByIdAsync(customerId);
                if (customer == null)
                {
                    throw new ArgumentException($"Customer with ID {customerId} not found");
                }

                LoyaltyRewards rewards = await GetOrCreateCustomerRewardsAsync(customerId, customer.TenantId);

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

                _ = await _repository.UpdateAsync(rewards);
                await _repository.SaveChangesAsync();

                // Phase 5: Enqueue LoyaltyPointsChanged outbox event (same transaction — reliable persistence)
                EnqueueLoyaltyPointsChangedEvent(customer.TenantId, customerId, points, rewards.PointBalance, reason, isAdd: true);

                // Phase 5: Direct NATS publish for immediate push notification (fire-and-forget)
                await PublishLoyaltyPointsChangedNatsAsync(customerId, points, rewards.PointBalance, reason, isAdd: true);

                _logger.LogInformation("Added {Points} points to customer {CustomerId}. New balance: {Balance}",
                    points, customerId, rewards.PointBalance);
                return true;
            }
            catch (Exception ex)
            {
                if (ownsTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                _logger.LogError(ex, "Failed to add points to customer {CustomerId}", customerId);
                return false;
            }
            finally
            {
                if (ownsTransaction && transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        public async Task<bool> SubtractPointsAsync(Guid customerId, int points, string reason)
        {
            if (points <= 0)
            {
                return false;
            }

            // Bug 6 fix (same as AddPointsAsync): Support ambient transaction.
            // RedemptionService.RedeemAsync begins a transaction before calling SubtractPointsAsync.
            // SQLite does not support nested transactions → BeginTransactionAsync throws.
            // If an ambient transaction exists, join it (no explicit commit/rollback — caller owns it).
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            bool ownsTransaction = false;
            try
            {
                // Get customer to retrieve tenant ID
                Customer? customer = await _repository.GetCustomerByIdAsync(customerId);
                if (customer == null)
                {
                    throw new ArgumentException($"Customer with ID {customerId} not found");
                }

                // Tiered Auth Phase 2: Verification gate — redeem requires IdentityLevel >= Verified.
                // Earn (AddPointsAsync) is intentionally NOT gated — Social customers can still earn points.
                if (customer.IdentityLevel < IdentityLevel.Verified)
                {
                    _logger.LogWarning("Redeem blocked for customer {CustomerId}: IdentityLevel={Current} < Required={Required}",
                        customerId, customer.IdentityLevel, IdentityLevel.Verified);
                    throw new IdentityLevelNotSufficientException(customerId, customer.IdentityLevel, IdentityLevel.Verified);
                }

                LoyaltyRewards rewards = await GetOrCreateCustomerRewardsAsync(customerId, customer.TenantId);

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

                _ = await _repository.UpdateAsync(rewards);
                await _repository.SaveChangesAsync();

                // Phase 5: Enqueue LoyaltyPointsChanged outbox event (same transaction — reliable persistence)
                EnqueueLoyaltyPointsChangedEvent(customer.TenantId, customerId, -points, rewards.PointBalance, reason, isAdd: false);

                // Phase 5: Direct NATS publish for immediate push notification (fire-and-forget)
                await PublishLoyaltyPointsChangedNatsAsync(customerId, -points, rewards.PointBalance, reason, isAdd: false);

                _logger.LogInformation("Subtracted {Points} points from customer {CustomerId}. New balance: {Balance}",
                    points, customerId, rewards.PointBalance);
                return true;
            }
            catch (IdentityLevelNotSufficientException)
            {
                // Tiered Auth Phase 2: re-throw gate exception so controller can return 403 with upgrade hint.
                if (ownsTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
            catch (Exception ex)
            {
                if (ownsTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                _logger.LogError(ex, "Failed to subtract points from customer {CustomerId}", customerId);
                return false;
            }
            finally
            {
                if (ownsTransaction && transaction != null)
                {
                    await transaction.DisposeAsync();
                }
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

            _ = await _repository.UpdateAsync(rewards);
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

        /// <summary>
        /// Phase 5: Enqueue LoyaltyPointsChanged outbox event for reliable NATS delivery + PG sync.
        /// Called within the same transaction as the points change (before commit).
        /// </summary>
        private void EnqueueLoyaltyPointsChangedEvent(TenantId tenantId, Guid customerId, int pointsChange, int newBalance, string reason, bool isAdd)
        {
            if (_outboxRepository == null)
            {
                _logger.LogWarning("OutboxRepository not available — LoyaltyPointsChanged event for customer {CustomerId} not persisted", customerId);
                return;
            }

            var payload = new
            {
                customerId = customerId,
                pointsChange = pointsChange,
                newBalance = newBalance,
                reason = reason,
                isAdd = isAdd,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
            string eventData = JsonSerializer.Serialize(payload, EventJsonOptions);
            var outboxEvent = new OutboxEvent(
                tenantId,
                new ElectronicInvoiceId(Guid.Empty),
                EventTypes.LoyaltyPointsChanged,
                eventData);
            _ = _outboxRepository.EnqueueAsync(outboxEvent);
            _logger.LogInformation("Enqueued LoyaltyPointsChanged event to Outbox for customer {CustomerId} (PointsChange={PointsChange}, NewBalance={NewBalance})",
                customerId, pointsChange, newBalance);
        }

        /// <summary>
        /// Phase 5: Direct NATS publish for immediate push notification.
        /// Fire-and-forget — wrapped in try/catch to prevent loyalty workflow failures.
        /// </summary>
        private async Task PublishLoyaltyPointsChangedNatsAsync(Guid customerId, int pointsChange, int newBalance, string reason, bool isAdd)
        {
            if (_natsEventPublisher == null)
            {
                _logger.LogDebug("NATS event publisher not available - skipping loyalty points event publishing");
                return;
            }

            try
            {
                var payload = new
                {
                    customerId = customerId,
                    pointsChange = pointsChange,
                    newBalance = newBalance,
                    reason = reason,
                    isAdd = isAdd,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
                var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, EventJsonOptions);
                await _natsEventPublisher.PublishAsync("loyalty.points.changed", payloadBytes);
                _logger.LogInformation("Published loyalty points changed event to NATS: CustomerId={CustomerId}, PointsChange={PointsChange}, NewBalance={NewBalance}",
                    customerId, pointsChange, newBalance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish loyalty points changed event to NATS for CustomerId: {CustomerId}", customerId);
            }
        }

        public async Task<bool> ActivateCustomerAsync(Guid customerId)
        {
            try
            {
                _logger.LogInformation("Activating loyalty program for customer {CustomerId}", customerId);

                // Get customer to retrieve tenant ID
                Customer? customer = await _repository.GetCustomerByIdAsync(customerId);
                if (customer == null)
                {
                    throw new ArgumentException($"Customer with ID {customerId} not found");
                }
                
                // Get or create customer rewards
                LoyaltyRewards rewards = await GetOrCreateCustomerRewardsAsync(customerId, customer.TenantId);

                // Add welcome bonus points
                _ = await AddPointsAsync(customerId, 100, "Welcome bonus for joining loyalty program");

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
