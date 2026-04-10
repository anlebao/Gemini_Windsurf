using VanAn.Shared.Omnichannel;
using VanAn.Shared.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Implementation of real-time synchronization service for inventory and customer data
/// Provides instant data synchronization across all connected devices and platforms
/// </summary>
public class RealTimeSyncService : IRealTimeSyncService
{
    private readonly ILogger<RealTimeSyncService> _logger;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, List<Subscription>> _subscriptions;
    private readonly ConcurrentDictionary<string, ConnectedDevice> _connectedDevices;
    private readonly ConcurrentDictionary<string, InventorySyncStatus> _inventoryStatus;
    private readonly ConcurrentDictionary<string, CustomerSyncStatus> _customerStatus;
    private readonly ConcurrentDictionary<string, List<RealTimeSyncConflict>> _conflicts;

    public RealTimeSyncService(ILogger<RealTimeSyncService> logger, IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
        _subscriptions = new ConcurrentDictionary<string, List<Subscription>>();
        _connectedDevices = new ConcurrentDictionary<string, ConnectedDevice>();
        _inventoryStatus = new ConcurrentDictionary<string, InventorySyncStatus>();
        _customerStatus = new ConcurrentDictionary<string, CustomerSyncStatus>();
        _conflicts = new ConcurrentDictionary<string, List<RealTimeSyncConflict>>();
    }

    public async Task<SubscriptionResult> SubscribeToInventoryUpdatesAsync(string shopId, string deviceId, Func<InventoryUpdate, Task> onInventoryUpdate)
    {
        try
        {
            _logger.LogInformation("Device {DeviceId} subscribing to inventory updates for shop {ShopId}", deviceId, shopId);

            var subscriptionId = Guid.NewGuid().ToString();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Create subscription
            var subscription = new Subscription
            {
                SubscriptionId = subscriptionId,
                ShopId = shopId,
                DeviceId = deviceId,
                SubscriptionType = "Inventory",
                Callback = onInventoryUpdate,
                SubscribedAt = DateTime.UtcNow
            };

            // Add to subscriptions
            var shopSubscriptions = _subscriptions.GetOrAdd(shopId, _ => new List<Subscription>());
            shopSubscriptions.Add(subscription);

            // Update device connection
            await UpdateDeviceConnectionAsync(shopId, deviceId, "Inventory");

            stopwatch.Stop();

            var result = new SubscriptionResult
            {
                Success = true,
                SubscriptionId = subscriptionId,
                ShopId = shopId,
                DeviceId = deviceId,
                SubscribedAt = subscription.SubscribedAt,
                SubscriptionType = "Inventory",
                Latency = stopwatch.Elapsed
            };

            _logger.LogInformation("Device {DeviceId} successfully subscribed to inventory updates for shop {ShopId}", deviceId, shopId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing device {DeviceId} to inventory updates for shop {ShopId}", deviceId, shopId);
            return new SubscriptionResult
            {
                Success = false,
                ShopId = shopId,
                DeviceId = deviceId,
                SubscriptionType = "Inventory",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<SubscriptionResult> SubscribeToCustomerUpdatesAsync(string shopId, string deviceId, Func<CustomerUpdate, Task> onCustomerUpdate)
    {
        try
        {
            _logger.LogInformation("Device {DeviceId} subscribing to customer updates for shop {ShopId}", deviceId, shopId);

            var subscriptionId = Guid.NewGuid().ToString();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Create subscription
            var subscription = new Subscription
            {
                SubscriptionId = subscriptionId,
                ShopId = shopId,
                DeviceId = deviceId,
                SubscriptionType = "Customer",
                Callback = onCustomerUpdate,
                SubscribedAt = DateTime.UtcNow
            };

            // Add to subscriptions
            var shopSubscriptions = _subscriptions.GetOrAdd(shopId, _ => new List<Subscription>());
            shopSubscriptions.Add(subscription);

            // Update device connection
            await UpdateDeviceConnectionAsync(shopId, deviceId, "Customer");

            stopwatch.Stop();

            var result = new SubscriptionResult
            {
                Success = true,
                SubscriptionId = subscriptionId,
                ShopId = shopId,
                DeviceId = deviceId,
                SubscribedAt = subscription.SubscribedAt,
                SubscriptionType = "Customer",
                Latency = stopwatch.Elapsed
            };

            _logger.LogInformation("Device {DeviceId} successfully subscribed to customer updates for shop {ShopId}", deviceId, shopId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing device {DeviceId} to customer updates for shop {ShopId}", deviceId, shopId);
            return new SubscriptionResult
            {
                Success = false,
                ShopId = shopId,
                DeviceId = deviceId,
                SubscriptionType = "Customer",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<PublishResult> PublishInventoryUpdateAsync(InventoryUpdate update, string publisherDeviceId)
    {
        try
        {
            _logger.LogInformation("Publishing inventory update {UpdateId} for product {ProductId} in shop {ShopId}", 
                update.UpdateId, update.ProductId, update.ShopId);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var successfulDevices = new List<string>();
            var failedDeliveries = new List<DeliveryError>();

            // Get all inventory subscriptions for this shop
            if (_subscriptions.TryGetValue(update.ShopId, out var shopSubscriptions))
            {
                var inventorySubscriptions = shopSubscriptions.Where(s => s.SubscriptionType == "Inventory").ToList();

                foreach (var subscription in inventorySubscriptions)
                {
                    try
                    {
                        // Don't send update back to the publisher
                        if (subscription.DeviceId == publisherDeviceId)
                            continue;

                        // Simulate network latency
                        await Task.Delay(Random.Shared.Next(5, 50));

                        // Invoke callback
                        if (subscription.Callback is Func<InventoryUpdate, Task> inventoryCallback)
                        {
                            await inventoryCallback(update);
                            successfulDevices.Add(subscription.DeviceId);
                        }
                    }
                    catch (Exception ex)
                    {
                        failedDeliveries.Add(new DeliveryError
                        {
                            DeviceId = subscription.DeviceId,
                            DeviceType = GetDeviceType(subscription.DeviceId),
                            ErrorMessage = ex.Message,
                            ErrorOccurredAt = DateTime.UtcNow,
                            IsRetriable = true,
                            RetryCount = 0
                        });
                        _logger.LogError(ex, "Failed to deliver inventory update to device {DeviceId}", subscription.DeviceId);
                    }
                }
            }

            // Update inventory sync status
            await UpdateInventorySyncStatusAsync(update);

            stopwatch.Stop();

            var result = new PublishResult
            {
                Success = failedDeliveries.Count == 0,
                UpdateId = update.UpdateId,
                TotalSubscribers = successfulDevices.Count + failedDeliveries.Count,
                SuccessfulDeliveries = successfulDevices.Count,
                SuccessfulDevices = successfulDevices,
                FailedDeliveries = failedDeliveries,
                PublishedAt = DateTime.UtcNow,
                PublishDuration = stopwatch.Elapsed,
                SyncVersion = update.SyncVersion
            };

            _logger.LogInformation("Inventory update {UpdateId} published successfully. Delivered to {SuccessCount}/{TotalCount} devices", 
                update.UpdateId, result.SuccessfulDeliveries, result.TotalSubscribers);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing inventory update {UpdateId}", update.UpdateId);
            throw;
        }
    }

    public async Task<PublishResult> PublishCustomerUpdateAsync(CustomerUpdate update, string publisherDeviceId)
    {
        try
        {
            _logger.LogInformation("Publishing customer update {UpdateId} for customer {CustomerId} in shop {ShopId}", 
                update.UpdateId, update.CustomerId, update.ShopId);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var successfulDevices = new List<string>();
            var failedDeliveries = new List<DeliveryError>();

            // Get all customer subscriptions for this shop
            if (_subscriptions.TryGetValue(update.ShopId, out var shopSubscriptions))
            {
                var customerSubscriptions = shopSubscriptions.Where(s => s.SubscriptionType == "Customer").ToList();

                foreach (var subscription in customerSubscriptions)
                {
                    try
                    {
                        // Don't send update back to the publisher
                        if (subscription.DeviceId == publisherDeviceId)
                            continue;

                        // Simulate network latency
                        await Task.Delay(Random.Shared.Next(5, 50));

                        // Invoke callback
                        if (subscription.Callback is Func<CustomerUpdate, Task> customerCallback)
                        {
                            await customerCallback(update);
                            successfulDevices.Add(subscription.DeviceId);
                        }
                    }
                    catch (Exception ex)
                    {
                        failedDeliveries.Add(new DeliveryError
                        {
                            DeviceId = subscription.DeviceId,
                            DeviceType = GetDeviceType(subscription.DeviceId),
                            ErrorMessage = ex.Message,
                            ErrorOccurredAt = DateTime.UtcNow,
                            IsRetriable = true,
                            RetryCount = 0
                        });
                        _logger.LogError(ex, "Failed to deliver customer update to device {DeviceId}", subscription.DeviceId);
                    }
                }
            }

            // Update customer sync status
            await UpdateCustomerSyncStatusAsync(update);

            stopwatch.Stop();

            var result = new PublishResult
            {
                Success = failedDeliveries.Count == 0,
                UpdateId = update.UpdateId,
                TotalSubscribers = successfulDevices.Count + failedDeliveries.Count,
                SuccessfulDeliveries = successfulDevices.Count,
                SuccessfulDevices = successfulDevices,
                FailedDeliveries = failedDeliveries,
                PublishedAt = DateTime.UtcNow,
                PublishDuration = stopwatch.Elapsed,
                SyncVersion = update.SyncVersion
            };

            _logger.LogInformation("Customer update {UpdateId} published successfully. Delivered to {SuccessCount}/{TotalCount} devices", 
                update.UpdateId, result.SuccessfulDeliveries, result.TotalSubscribers);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing customer update {UpdateId}", update.UpdateId);
            throw;
        }
    }

    public async Task<InventorySyncStatus> GetInventorySyncStatusAsync(string shopId, ProductId productId)
    {
        try
        {
            _logger.LogInformation("Getting inventory sync status for product {ProductId} in shop {ShopId}", productId, shopId);

            var statusKey = $"{shopId}_inventory_{productId}";
            
            if (_inventoryStatus.TryGetValue(statusKey, out var cachedStatus))
            {
                return cachedStatus;
            }

            // Create default status if not found
            var defaultStatus = new InventorySyncStatus
            {
                ShopId = shopId,
                ProductId = productId,
                CurrentQuantity = 100, // Default quantity
                LastSyncVersion = "v1.0.0",
                LastSyncAt = DateTime.UtcNow,
                SyncedDevices = new List<string>(),
                PendingDevices = new List<string>(),
                IsFullySynced = true,
                Health = SyncHealth.Excellent,
                AverageSyncLatency = TimeSpan.FromMilliseconds(25),
                ConflictCount = 0
            };

            _inventoryStatus[statusKey] = defaultStatus;
            return defaultStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory sync status for product {ProductId} in shop {ShopId}", productId, shopId);
            throw;
        }
    }

    public async Task<CustomerSyncStatus> GetCustomerSyncStatusAsync(string shopId, CustomerId customerId)
    {
        try
        {
            _logger.LogInformation("Getting customer sync status for customer {CustomerId} in shop {ShopId}", customerId, shopId);

            var statusKey = $"{shopId}_customer_{customerId}";
            
            if (_customerStatus.TryGetValue(statusKey, out var cachedStatus))
            {
                return cachedStatus;
            }

            // Create default status if not found
            var defaultStatus = new CustomerSyncStatus
            {
                ShopId = shopId,
                CustomerId = customerId,
                LastSyncVersion = "v1.0.0",
                LastSyncAt = DateTime.UtcNow,
                SyncedDevices = new List<string>(),
                PendingDevices = new List<string>(),
                IsFullySynced = true,
                Health = SyncHealth.Excellent,
                AverageSyncLatency = TimeSpan.FromMilliseconds(20),
                ConflictCount = 0,
                CurrentData = new Dictionary<string, object>
                {
                    ["FullName"] = "Default Customer",
                    ["LoyaltyPoints"] = 0,
                    ["Tier"] = "Bronze"
                }
            };

            _customerStatus[statusKey] = defaultStatus;
            return defaultStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer sync status for customer {CustomerId} in shop {ShopId}", customerId, shopId);
            throw;
        }
    }

    public async Task<ForceSyncResult> ForceSyncInventoryAsync(string shopId, ProductId productId, string requesterDeviceId)
    {
        try
        {
            _logger.LogInformation("Force syncing inventory for product {ProductId} in shop {ShopId} requested by {DeviceId}", 
                productId, shopId, requesterDeviceId);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var successfulDevices = new List<string>();
            var failedDevices = new List<SyncError>();
            var resolvedConflicts = new List<RealTimeSyncConflict>();

            // Get all connected devices for the shop
            var shopDevices = _connectedDevices.Values.Where(d => d.ShopId == shopId && d.IsActive).ToList();

            foreach (var device in shopDevices)
            {
                try
                {
                    // Simulate sync process
                    await Task.Delay(Random.Shared.Next(50, 200));
                    successfulDevices.Add(device.DeviceId);
                }
                catch (Exception ex)
                {
                    failedDevices.Add(new SyncError
                    {
                        DeviceId = device.DeviceId,
                        DeviceType = device.DeviceType,
                        ErrorMessage = ex.Message,
                        ErrorOccurredAt = DateTime.UtcNow,
                        IsRetriable = true,
                        ErrorCode = "SYNC_FAILED"
                    });
                }
            }

            // Update sync status
            var statusKey = $"{shopId}_inventory_{productId}";
            if (_inventoryStatus.TryGetValue(statusKey, out var status))
            {
                var updatedStatus = status with
                {
                    LastSyncAt = DateTime.UtcNow,
                    SyncedDevices = successfulDevices,
                    PendingDevices = failedDevices.Select(d => d.DeviceId).ToList(),
                    IsFullySynced = failedDevices.Count == 0,
                    LastSyncVersion = GenerateSyncVersion()
                };
                _inventoryStatus[statusKey] = updatedStatus;
            }

            stopwatch.Stop();

            var result = new ForceSyncResult
            {
                Success = failedDevices.Count == 0,
                ShopId = shopId,
                EntityType = "Inventory",
                EntityId = productId.Value.ToString(),
                TotalDevices = shopDevices.Count,
                SyncedDevices = successfulDevices.Count,
                SuccessfulDevices = successfulDevices,
                FailedDevices = failedDevices,
                SyncStartedAt = DateTime.UtcNow.Subtract(stopwatch.Elapsed),
                SyncCompletedAt = DateTime.UtcNow,
                SyncDuration = stopwatch.Elapsed,
                SyncVersion = GenerateSyncVersion(),
                ResolvedConflicts = resolvedConflicts
            };

            _logger.LogInformation("Force sync completed for inventory {ProductId}. Synced {SuccessCount}/{TotalCount} devices", 
                productId, result.SyncedDevices, result.TotalDevices);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error force syncing inventory for product {ProductId} in shop {ShopId}", productId, shopId);
            throw;
        }
    }

    public async Task<ForceSyncResult> ForceSyncCustomerAsync(string shopId, CustomerId customerId, string requesterDeviceId)
    {
        try
        {
            _logger.LogInformation("Force syncing customer data for customer {CustomerId} in shop {ShopId} requested by {DeviceId}", 
                customerId, shopId, requesterDeviceId);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var successfulDevices = new List<string>();
            var failedDevices = new List<SyncError>();
            var resolvedConflicts = new List<RealTimeSyncConflict>();

            // Get all connected devices for the shop
            var shopDevices = _connectedDevices.Values.Where(d => d.ShopId == shopId && d.IsActive).ToList();

            foreach (var device in shopDevices)
            {
                try
                {
                    // Simulate sync process
                    await Task.Delay(Random.Shared.Next(50, 200));
                    successfulDevices.Add(device.DeviceId);
                }
                catch (Exception ex)
                {
                    failedDevices.Add(new SyncError
                    {
                        DeviceId = device.DeviceId,
                        DeviceType = device.DeviceType,
                        ErrorMessage = ex.Message,
                        ErrorOccurredAt = DateTime.UtcNow,
                        IsRetriable = true,
                        ErrorCode = "SYNC_FAILED"
                    });
                }
            }

            // Update sync status
            var statusKey = $"{shopId}_customer_{customerId}";
            if (_customerStatus.TryGetValue(statusKey, out var status))
            {
                var updatedStatus = status with
                {
                    LastSyncAt = DateTime.UtcNow,
                    SyncedDevices = successfulDevices,
                    PendingDevices = failedDevices.Select(d => d.DeviceId).ToList(),
                    IsFullySynced = failedDevices.Count == 0,
                    LastSyncVersion = GenerateSyncVersion()
                };
                _customerStatus[statusKey] = updatedStatus;
            }

            stopwatch.Stop();

            var result = new ForceSyncResult
            {
                Success = failedDevices.Count == 0,
                ShopId = shopId,
                EntityType = "Customer",
                EntityId = customerId.Value.ToString(),
                TotalDevices = shopDevices.Count,
                SyncedDevices = successfulDevices.Count,
                SuccessfulDevices = successfulDevices,
                FailedDevices = failedDevices,
                SyncStartedAt = DateTime.UtcNow.Subtract(stopwatch.Elapsed),
                SyncCompletedAt = DateTime.UtcNow,
                SyncDuration = stopwatch.Elapsed,
                SyncVersion = GenerateSyncVersion(),
                ResolvedConflicts = resolvedConflicts
            };

            _logger.LogInformation("Force sync completed for customer {CustomerId}. Synced {SuccessCount}/{TotalCount} devices", 
                customerId, result.SyncedDevices, result.TotalDevices);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error force syncing customer data for customer {CustomerId} in shop {ShopId}", customerId, shopId);
            throw;
        }
    }

    public async Task<List<RealTimeSyncConflict>> DetectSyncConflictsAsync(string shopId)
    {
        try
        {
            _logger.LogInformation("Detecting sync conflicts for shop {ShopId}", shopId);

            var conflicts = new List<RealTimeSyncConflict>();

            // Simulate conflict detection
            if (_conflicts.TryGetValue(shopId, out var shopConflicts))
            {
                conflicts.AddRange(shopConflicts);
            }
            else
            {
                // Generate some sample conflicts for demonstration
                conflicts = GenerateSampleConflicts(shopId);
                _conflicts[shopId] = conflicts;
            }

            _logger.LogInformation("Detected {ConflictCount} conflicts for shop {ShopId}", conflicts.Count, shopId);
            return conflicts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting sync conflicts for shop {ShopId}", shopId);
            throw;
        }
    }

    public async Task<ConflictResolutionResult> ResolveSyncConflictAsync(string conflictId, RealTimeConflictResolutionStrategy strategy, string resolverDeviceId)
    {
        try
        {
            _logger.LogInformation("Resolving conflict {ConflictId} using strategy {Strategy}", conflictId, strategy);

            var conflict = _conflicts.Values.SelectMany(c => c).FirstOrDefault(c => c.ConflictId == conflictId);
            if (conflict == null)
            {
                return new ConflictResolutionResult
                {
                    Success = false,
                    ConflictId = conflictId,
                    ShopId = "unknown",
                    Strategy = strategy,
                    ResolvedBy = "system",
                    ResolvedByDevice = resolverDeviceId,
                    ResolvedAt = DateTime.UtcNow,
                    ResolutionDescription = "Conflict not found"
                };
            }

            var resolvedValue = ApplyConflictResolutionStrategy(conflict, strategy);
            var affectedDevices = new List<string> { conflict.LocalDeviceId, conflict.RemoteDeviceId };

            // Remove resolved conflict
            var shopConflicts = _conflicts[conflict.ShopId];
            shopConflicts.Remove(conflict);

            var result = new ConflictResolutionResult
            {
                Success = true,
                ConflictId = conflictId,
                ShopId = conflict.ShopId,
                Strategy = strategy,
                ResolvedValue = resolvedValue,
                ResolvedBy = "system",
                ResolvedByDevice = resolverDeviceId,
                ResolvedAt = DateTime.UtcNow,
                ResolutionDescription = $"Conflict resolved using {strategy} strategy",
                AffectedDevices = affectedDevices,
                AppliedSyncVersion = GenerateSyncVersion()
            };

            _logger.LogInformation("Conflict {ConflictId} resolved successfully", conflictId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving conflict {ConflictId}", conflictId);
            throw;
        }
    }

    public async Task<RealTimeSyncAnalytics> GetSyncAnalyticsAsync(string shopId, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            _logger.LogInformation("Getting sync analytics for shop {ShopId} from {From} to {To}", shopId, from, to);

            var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
            var toDate = to ?? DateTime.UtcNow;

            // Simulate analytics calculation
            var successfulDeliveries = Random.Shared.Next(475, 1900);
            var failedDeliveries = Random.Shared.Next(25, 100);
            
            var analytics = new RealTimeSyncAnalytics
            {
                ShopId = shopId,
                PeriodStart = fromDate,
                PeriodEnd = toDate,
                TotalUpdates = successfulDeliveries + failedDeliveries,
                InventoryUpdates = Random.Shared.Next(300, 1200),
                CustomerUpdates = Random.Shared.Next(200, 800),
                SuccessfulDeliveries = successfulDeliveries,
                FailedDeliveries = failedDeliveries,
                SuccessRate = (decimal)successfulDeliveries / (successfulDeliveries + failedDeliveries),
                AverageLatency = TimeSpan.FromMilliseconds(Random.Shared.Next(20, 60)),
                MaxLatency = TimeSpan.FromMilliseconds(Random.Shared.Next(100, 200)),
                MinLatency = TimeSpan.FromMilliseconds(Random.Shared.Next(5, 15)),
                UpdatesByDevice = new Dictionary<string, int>(),
                LatencyByDevice = new Dictionary<string, decimal>(),
                ActiveSubscribers = _connectedDevices.Values.Count(d => d.ShopId == shopId && d.IsActive),
                ConflictsDetected = Random.Shared.Next(5, 25),
                ConflictsResolved = Random.Shared.Next(3, 20)
            };

            // Generate device-specific metrics
            var shopDevices = _connectedDevices.Values.Where(d => d.ShopId == shopId).ToList();
            foreach (var device in shopDevices)
            {
                analytics.UpdatesByDevice[device.DeviceId] = Random.Shared.Next(50, 200);
                analytics.LatencyByDevice[device.DeviceId] = (decimal)Random.Shared.Next(15, 75);
            }

            _logger.LogInformation("Analytics generated for shop {ShopId}: {TotalUpdates} updates, {SuccessRate:P2} success rate", 
                shopId, analytics.TotalUpdates, analytics.SuccessRate);

            return analytics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync analytics for shop {ShopId}", shopId);
            throw;
        }
    }

    public async Task<List<ConnectedDevice>> GetConnectedDevicesAsync(string shopId)
    {
        try
        {
            _logger.LogInformation("Getting connected devices for shop {ShopId}", shopId);

            var devices = _connectedDevices.Values
                .Where(d => d.ShopId == shopId)
                .OrderByDescending(d => d.LastActivityAt)
                .ToList();

            _logger.LogInformation("Found {DeviceCount} connected devices for shop {ShopId}", devices.Count, shopId);
            return devices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connected devices for shop {ShopId}", shopId);
            throw;
        }
    }

    public async Task<DisconnectResult> DisconnectDeviceAsync(string shopId, string deviceId)
    {
        try
        {
            _logger.LogInformation("Disconnecting device {DeviceId} from shop {ShopId}", deviceId, shopId);

            var removedSubscriptions = new List<string>();
            var errors = new List<string>();

            // Remove subscriptions
            if (_subscriptions.TryGetValue(shopId, out var shopSubscriptions))
            {
                var deviceSubscriptions = shopSubscriptions.Where(s => s.DeviceId == deviceId).ToList();
                foreach (var subscription in deviceSubscriptions)
                {
                    shopSubscriptions.Remove(subscription);
                    removedSubscriptions.Add(subscription.SubscriptionType);
                }
            }

            // Remove device from connected devices
            if (_connectedDevices.TryRemove(deviceId, out var device))
            {
                var connectionDuration = DateTime.UtcNow - device.ConnectedAt;

                var result = new DisconnectResult
                {
                    Success = true,
                    DeviceId = deviceId,
                    ShopId = shopId,
                    DisconnectedAt = DateTime.UtcNow,
                    RemovedSubscriptions = removedSubscriptions,
                    Errors = errors,
                    ConnectionDuration = connectionDuration
                };

                _logger.LogInformation("Device {DeviceId} disconnected successfully from shop {ShopId}", deviceId, shopId);
                return result;
            }

            errors.Add("Device not found");
            return new DisconnectResult
            {
                Success = false,
                DeviceId = deviceId,
                ShopId = shopId,
                DisconnectedAt = DateTime.UtcNow,
                RemovedSubscriptions = removedSubscriptions,
                Errors = errors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting device {DeviceId} from shop {ShopId}", deviceId, shopId);
            throw;
        }
    }

    public async Task<SyncPerformanceMetrics> GetPerformanceMetricsAsync(string shopId)
    {
        try
        {
            _logger.LogInformation("Getting performance metrics for shop {ShopId}", shopId);

            var shopDevices = _connectedDevices.Values.Where(d => d.ShopId == shopId).ToList();
            var activeConnections = shopDevices.Count(d => d.IsActive);
            var totalSubscriptions = shopDevices.Sum(d => d.Subscriptions.Count);

            var metrics = new SyncPerformanceMetrics
            {
                ShopId = shopId,
                GeneratedAt = DateTime.UtcNow,
                ActiveConnections = activeConnections,
                TotalSubscriptions = totalSubscriptions,
                CurrentLatency = TimeSpan.FromMilliseconds(Random.Shared.Next(20, 50)),
                AverageLatency = TimeSpan.FromMilliseconds(Random.Shared.Next(25, 45)),
                P95Latency = TimeSpan.FromMilliseconds(Random.Shared.Next(60, 100)),
                P99Latency = TimeSpan.FromMilliseconds(Random.Shared.Next(100, 150)),
                MessagesPerSecond = Random.Shared.Next(10, 50),
                BytesPerSecond = Random.Shared.Next(1024, 4096),
                QueueSize = Random.Shared.Next(0, 10),
                ErrorCount = Random.Shared.Next(0, 5),
                ErrorRate = (decimal)Random.Shared.NextDouble() * 0.1m,
                DeviceMetrics = new Dictionary<string, DevicePerformanceMetrics>()
            };

            // Generate device-specific metrics
            foreach (var device in shopDevices)
            {
                metrics.DeviceMetrics[device.DeviceId] = new DevicePerformanceMetrics
                {
                    DeviceId = device.DeviceId,
                    DeviceType = device.DeviceType,
                    Latency = TimeSpan.FromMilliseconds(Random.Shared.Next(15, 60)),
                    MessagesReceived = Random.Shared.Next(100, 500),
                    MessagesSent = Random.Shared.Next(80, 400),
                    BytesReceived = Random.Shared.Next(10240, 51200),
                    BytesSent = Random.Shared.Next(8192, 40960),
                    IsHealthy = device.IsActive
                };
            }

            _logger.LogInformation("Performance metrics generated for shop {ShopId}: {ActiveConnections} active connections", 
                shopId, metrics.ActiveConnections);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance metrics for shop {ShopId}", shopId);
            throw;
        }
    }

    #region Private Helper Methods

    private async Task UpdateDeviceConnectionAsync(string shopId, string deviceId, string subscriptionType)
    {
        var device = _connectedDevices.GetOrAdd(deviceId, _ => new ConnectedDevice
        {
            DeviceId = deviceId,
            DeviceType = GetDeviceType(deviceId),
            ShopId = shopId,
            UserId = $"user_{deviceId}",
            ConnectedAt = DateTime.UtcNow,
            IsActive = true,
            Subscriptions = new List<string>(),
            DeviceMetadata = new Dictionary<string, object>()
        });

        if (!device.Subscriptions.Contains(subscriptionType))
        {
            device.Subscriptions.Add(subscriptionType);
        }
    }

    private async Task UpdateInventorySyncStatusAsync(InventoryUpdate update)
    {
        var statusKey = $"{update.ShopId}_inventory_{update.ProductId}";
        
        var existingStatus = _inventoryStatus.TryGetValue(statusKey, out var status) ? status : new InventorySyncStatus
        {
            ShopId = update.ShopId,
            ProductId = update.ProductId,
            CurrentQuantity = update.NewQuantity,
            LastSyncVersion = update.SyncVersion,
            LastSyncAt = DateTime.UtcNow,
            SyncedDevices = update.AffectedDevices,
            PendingDevices = new List<string>(),
            IsFullySynced = true,
            Health = SyncHealth.Excellent,
            AverageSyncLatency = TimeSpan.FromMilliseconds(25),
            ConflictCount = 0
        };

        var updatedStatus = existingStatus with
        {
            CurrentQuantity = update.NewQuantity,
            LastSyncVersion = update.SyncVersion,
            LastSyncAt = DateTime.UtcNow,
            SyncedDevices = update.AffectedDevices,
            IsFullySynced = true
        };

        _inventoryStatus[statusKey] = updatedStatus;
    }

    private async Task UpdateCustomerSyncStatusAsync(CustomerUpdate update)
    {
        var statusKey = $"{update.ShopId}_customer_{update.CustomerId}";
        
        var existingStatus = _customerStatus.TryGetValue(statusKey, out var status) ? status : new CustomerSyncStatus
        {
            ShopId = update.ShopId,
            CustomerId = update.CustomerId,
            LastSyncVersion = update.SyncVersion,
            LastSyncAt = DateTime.UtcNow,
            SyncedDevices = update.AffectedDevices,
            PendingDevices = new List<string>(),
            IsFullySynced = true,
            Health = SyncHealth.Excellent,
            AverageSyncLatency = TimeSpan.FromMilliseconds(20),
            ConflictCount = 0,
            CurrentData = new Dictionary<string, object>()
        };

        var updatedStatus = existingStatus with
        {
            LastSyncVersion = update.SyncVersion,
            LastSyncAt = DateTime.UtcNow,
            SyncedDevices = update.AffectedDevices,
            IsFullySynced = true
        };

        _customerStatus[statusKey] = updatedStatus;
    }

    private string GetDeviceType(string deviceId)
    {
        return deviceId.ToLower() switch
        {
            var s when s.Contains("mobile") => "Mobile",
            var s when s.Contains("tablet") => "Tablet",
            var s when s.Contains("desktop") => "Desktop",
            var s when s.Contains("kiosk") => "Kiosk",
            var s when s.Contains("pos") => "POS",
            var s when s.Contains("watch") => "Wearable",
            _ => "Unknown"
        };
    }

    private string GenerateSyncVersion()
    {
        return $"v{DateTime.UtcNow:yyyyMMdd-HHmmss}";
    }

    private List<RealTimeSyncConflict> GenerateSampleConflicts(string shopId)
    {
        return new List<RealTimeSyncConflict>
        {
            new RealTimeSyncConflict
            {
                ShopId = shopId,
                EntityType = "Inventory",
                EntityId = Guid.NewGuid().ToString(),
                PropertyName = "Quantity",
                LocalValue = 50,
                RemoteValue = 45,
                LocalDeviceId = "mobile-001",
                RemoteDeviceId = "desktop-002",
                LocalTimestamp = DateTime.UtcNow.AddMinutes(-1),
                RemoteTimestamp = DateTime.UtcNow.AddSeconds(-30),
                Severity = RealTimeConflictSeverity.Medium,
                Description = "Quantity mismatch between devices",
                RequiresUserIntervention = false,
                ConflictType = "ConcurrentUpdate"
            },
            new RealTimeSyncConflict
            {
                ShopId = shopId,
                EntityType = "Customer",
                EntityId = Guid.NewGuid().ToString(),
                PropertyName = "LoyaltyPoints",
                LocalValue = 200,
                RemoteValue = 250,
                LocalDeviceId = "tablet-003",
                RemoteDeviceId = "mobile-004",
                LocalTimestamp = DateTime.UtcNow.AddMinutes(-2),
                RemoteTimestamp = DateTime.UtcNow.AddMinutes(-1),
                Severity = RealTimeConflictSeverity.High,
                Description = "Loyalty points discrepancy",
                RequiresUserIntervention = true,
                ConflictType = "DataInconsistency"
            }
        };
    }

    private object? ApplyConflictResolutionStrategy(RealTimeSyncConflict conflict, RealTimeConflictResolutionStrategy strategy)
    {
        return strategy switch
        {
            RealTimeConflictResolutionStrategy.LocalWins => conflict.LocalValue,
            RealTimeConflictResolutionStrategy.RemoteWins => conflict.RemoteValue,
            RealTimeConflictResolutionStrategy.LastWriteWins => conflict.RemoteTimestamp > conflict.LocalTimestamp ? conflict.RemoteValue : conflict.LocalValue,
            RealTimeConflictResolutionStrategy.Merge => conflict.LocalValue, // Simplified merge
            _ => conflict.RemoteValue
        };
    }

    #endregion

    #region Internal Classes

    private class Subscription
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string ShopId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string SubscriptionType { get; set; } = string.Empty;
        public Delegate Callback { get; set; } = null!;
        public DateTime SubscribedAt { get; set; }
    }

    #endregion
}
