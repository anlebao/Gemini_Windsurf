using VanAn.Shared.Omnichannel;
using VanAn.Shared.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Implementation of omnichannel order management service
    /// Provides cross-device order tracking, status synchronization, and workflow management
    /// </summary>
    public class OmnichannelOrderService(ILogger<OmnichannelOrderService> logger, IMemoryCache cache) : IOmnichannelOrderService
    {
        private readonly ILogger<OmnichannelOrderService> _logger = logger;
        private readonly IMemoryCache _cache = cache;
        private readonly Dictionary<Guid, OmnichannelOrder> _orderStorage = [];
        private readonly Dictionary<string, List<OrderHistoryEntry>> _orderHistory = [];
        private readonly Dictionary<Guid, List<OrderConflict>> _orderConflicts = [];

        public async Task<OmnichannelOrder> CreateOrderAsync(CreateOrderRequest request, string userId, string deviceId)
        {
            try
            {
                _logger.LogInformation("Creating omnichannel order for customer: {CustomerId}, user: {UserId}, device: {DeviceId}",
                    request.CustomerId, userId, deviceId);

                Guid orderId = Guid.NewGuid();
                OmnichannelOrder order = new()
                {
                    OrderId = orderId,
                    CustomerId = request.CustomerId,
                    Items = request.Items,
                    SubTotal = request.Items.Sum(item => item.SubTotal),
                    TotalVatAmount = request.Items.Sum(item => item.VatAmount),
                    TotalAmount = request.Items.Sum(item => item.TotalAmount),
                    Status = new OrderStatusId("pending"),
                    StatusDescription = "Pending confirmation",
                    CreatedBy = userId,
                    CreatedByDevice = deviceId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    LastUpdatedBy = userId,
                    LastUpdatedByDevice = deviceId,
                    PaymentMethod = request.PaymentMethod,
                    PaymentStatus = "Pending",
                    DeliveryAddress = request.DeliveryAddress,
                    Priority = request.Priority,
                    EstimatedDeliveryTime = request.EstimatedDeliveryTime,
                    Tags = request.Tags,
                    Metadata = request.Metadata,
                    IsSyncedAcrossDevices = false,
                    SyncVersion = "1.0.0",
                    DeviceTracking =
                    [
                        new OrderDeviceTracking
                        {
                            DeviceId = deviceId,
                            DeviceType = DetermineDeviceType(deviceId),
                            FirstAccessedAt = DateTime.UtcNow,
                            LastAccessedAt = DateTime.UtcNow,
                            LastAction = "CREATE",
                            IsActive = true
                        }
                    ],
                    WorkflowInfo = new OrderWorkflowInfo
                    {
                        CurrentStep = "PENDING",
                        CompletedSteps = [],
                        PendingSteps = ["CONFIRMED", "PREPARING", "READY", "DELIVERED"],
                        WorkflowStartedAt = DateTime.UtcNow,
                        IsWorkflowComplete = false
                    }
                };

                // Store order
                _orderStorage[orderId] = order;

                // Create history entry
                await CreateOrderHistoryEntryAsync(orderId, OrderHistoryAction.Created, "Order created", userId, deviceId);

                // Cache the order
                string cacheKey = $"order_{orderId}";
                _ = _cache.Set(cacheKey, order, TimeSpan.FromHours(24));

                _logger.LogInformation("Omnichannel order created successfully: {OrderId}", orderId);
                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating omnichannel order for customer: {CustomerId}", request.CustomerId);
                throw;
            }
        }

        public async Task<OmnichannelOrder?> GetOrderAsync(Guid orderId, string userId)
        {
            try
            {
                _logger.LogInformation("Getting omnichannel order: {OrderId} for user: {UserId}", orderId, userId);

                // Try cache first
                string cacheKey = $"order_{orderId}";
                if (_cache.TryGetValue(cacheKey, out OmnichannelOrder? cachedOrder))
                {
                    return cachedOrder;
                }

                // Fallback to storage
                if (_orderStorage.TryGetValue(orderId, out OmnichannelOrder? order))
                {
                    // Update device tracking
                    await UpdateDeviceTrackingAsync(orderId, userId, "GET");

                    // Cache for future requests
                    _ = _cache.Set(cacheKey, order, TimeSpan.FromHours(24));

                    return order;
                }

                _logger.LogWarning("Order not found: {OrderId}", orderId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting omnichannel order: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<OmnichannelOrder> UpdateOrderAsync(UpdateOrderRequest request, string userId, string deviceId)
        {
            try
            {
                _logger.LogInformation("Updating omnichannel order: {OrderId} by user: {UserId}", request.OrderId, userId);

                if (!_orderStorage.TryGetValue(request.OrderId, out OmnichannelOrder? existingOrder))
                {
                    throw new InvalidOperationException($"Order {request.OrderId} not found");
                }

                OmnichannelOrder updatedOrder = existingOrder with
                {
                    Items = request.Items ?? existingOrder.Items,
                    DeliveryAddress = request.DeliveryAddress ?? existingOrder.DeliveryAddress,
                    Priority = request.Priority ?? existingOrder.Priority,
                    EstimatedDeliveryTime = request.EstimatedDeliveryTime ?? existingOrder.EstimatedDeliveryTime,
                    Metadata = request.Metadata ?? existingOrder.Metadata,
                    Tags = request.Tags ?? existingOrder.Tags,
                    UpdatedAt = DateTime.UtcNow,
                    LastUpdatedBy = userId,
                    LastUpdatedByDevice = deviceId,
                    IsSyncedAcrossDevices = false
                };

                // Recalculate totals if items changed
                if (request.Items != null)
                {
                    updatedOrder = updatedOrder with
                    {
                        SubTotal = request.Items.Sum(item => item.SubTotal),
                        TotalVatAmount = request.Items.Sum(item => item.VatAmount),
                        TotalAmount = request.Items.Sum(item => item.TotalAmount)
                    };
                }

                // Update storage
                _orderStorage[request.OrderId] = updatedOrder;

                // Update device tracking
                await UpdateDeviceTrackingAsync(request.OrderId, userId, "UPDATE");

                // Create history entry
                await CreateOrderHistoryEntryAsync(request.OrderId, OrderHistoryAction.Updated,
                    "Order updated", userId, deviceId, new { request.Comment });

                // Update cache
                string cacheKey = $"order_{request.OrderId}";
                _ = _cache.Set(cacheKey, updatedOrder, TimeSpan.FromHours(24));

                _logger.LogInformation("Omnichannel order updated successfully: {OrderId}", request.OrderId);
                return updatedOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating omnichannel order: {OrderId}", request.OrderId);
                throw;
            }
        }

        public async Task<OrderStatusUpdateResult> UpdateOrderStatusAsync(Guid orderId, OrderStatusId status, string userId, string deviceId, string? comment = null)
        {
            try
            {
                _logger.LogInformation("Updating order status: {OrderId} to {Status} by user: {UserId}", orderId, status, userId);

                if (!_orderStorage.TryGetValue(orderId, out OmnichannelOrder? order))
                {
                    return new OrderStatusUpdateResult
                    {
                        Success = false,
                        OrderId = orderId,
                        Errors = ["Order not found"]
                    };
                }

                OrderStatusId previousStatus = order.Status;
                OmnichannelOrder updatedOrder = order with
                {
                    Status = status,
                    StatusDescription = GetStatusDescription(status),
                    UpdatedAt = DateTime.UtcNow,
                    LastUpdatedBy = userId,
                    LastUpdatedByDevice = deviceId,
                    IsSyncedAcrossDevices = false
                };

                // Update storage
                _orderStorage[orderId] = updatedOrder;

                // Update workflow info
                await UpdateWorkflowInfoAsync(orderId, status);

                // Sync across devices
                List<string> syncedDevices = await SyncOrderStatusAcrossDevicesAsync(orderId, status);

                // Create history entry
                await CreateOrderHistoryEntryAsync(orderId, OrderHistoryAction.StatusChanged,
                    $"Status changed from {previousStatus} to {status}", userId, deviceId,
                    new { PreviousStatus = previousStatus, NewStatus = status });

                // Update cache
                string cacheKey = $"order_{orderId}";
                _ = _cache.Set(cacheKey, updatedOrder, TimeSpan.FromHours(24));

                OrderStatusUpdateResult result = new()
                {
                    Success = true,
                    OrderId = orderId,
                    PreviousStatus = previousStatus,
                    NewStatus = status,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = userId,
                    UpdatedByDevice = deviceId,
                    SyncedDevices = syncedDevices,
                    Comment = comment
                };

                _logger.LogInformation("Order status updated successfully: {OrderId} to {Status}", orderId, status);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<List<OrderHistoryEntry>> GetOrderHistoryAsync(Guid orderId, string userId)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("Getting order history: {OrderId} for user: {UserId}", orderId, userId);

                string historyKey = $"history_{orderId}";
                if (_orderHistory.TryGetValue(historyKey, out List<OrderHistoryEntry>? history))
                {
                    return [.. history.OrderByDescending(entry => entry.Timestamp)];
                }

                _logger.LogWarning("No history found for order: {OrderId}", orderId);
                return [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order history: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<OrderSyncResult> SyncOrderAcrossDevicesAsync(Guid orderId, string userId)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("Syncing order across devices: {OrderId} for user: {UserId}", orderId, userId);

                if (!_orderStorage.TryGetValue(orderId, out OmnichannelOrder? order))
                {
                    return new OrderSyncResult
                    {
                        Success = false,
                        OrderId = orderId,
                        FailedDevices = [
                            new DeviceSyncError { DeviceId = "system", ErrorMessage = "Order not found" }
                        ]
                    };
                }

                System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
                List<string> successfulDevices = [];
                List<DeviceSyncError> failedDevices = [];

                // Get all devices that have accessed this order
                List<string> allDevices = order.DeviceTracking.Select(dt => dt.DeviceId).ToList();

                foreach (string? deviceId in allDevices)
                {
                    try
                    {
                        // Simulate sync to device
                        await Task.Delay(Random.Shared.Next(10, 50)); // Simulate network latency

                        successfulDevices.Add(deviceId);
                        _logger.LogDebug("Successfully synced order {OrderId} to device: {DeviceId}", orderId, deviceId);
                    }
                    catch (Exception ex)
                    {
                        failedDevices.Add(new DeviceSyncError
                        {
                            DeviceId = deviceId,
                            DeviceType = DetermineDeviceType(deviceId),
                            ErrorMessage = ex.Message,
                            ErrorOccurredAt = DateTime.UtcNow,
                            IsRetriable = true
                        });
                        _logger.LogError(ex, "Failed to sync order {OrderId} to device: {DeviceId}", orderId, deviceId);
                    }
                }

                stopwatch.Stop();

                // Update order sync status
                OmnichannelOrder updatedOrder = order with
                {
                    IsSyncedAcrossDevices = failedDevices.Count == 0,
                    SyncVersion = GenerateSyncVersion(),
                    UpdatedAt = DateTime.UtcNow
                };
                _orderStorage[orderId] = updatedOrder;

                OrderSyncResult result = new()
                {
                    Success = failedDevices.Count == 0,
                    OrderId = orderId,
                    TotalDevices = allDevices.Count,
                    SyncedDevices = successfulDevices.Count,
                    SuccessfulDevices = successfulDevices,
                    FailedDevices = failedDevices,
                    SyncStartedAt = DateTime.UtcNow.Subtract(stopwatch.Elapsed),
                    SyncCompletedAt = DateTime.UtcNow,
                    SyncDuration = stopwatch.Elapsed,
                    SyncVersion = updatedOrder.SyncVersion
                };

                _logger.LogInformation("Order sync completed: {OrderId}, Success: {Success}, Synced: {SyncedCount}/{TotalCount}",
                    orderId, result.Success, result.SyncedDevices, result.TotalDevices);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing order across devices: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<List<OmnichannelOrder>> GetOrdersByStatusAsync(OrderStatusId status, string userId, string? deviceId = null)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("Getting orders by status: {Status} for user: {UserId}, device: {DeviceId}", status, userId, deviceId);

                List<OmnichannelOrder> orders = _orderStorage.Values
                    .Where(order => order.Status == status)
                    .Where(order => string.IsNullOrEmpty(deviceId) || order.DeviceTracking.Any(dt => dt.DeviceId == deviceId))
                    .ToList();

                _logger.LogInformation("Found {OrderCount} orders with status: {Status}", orders.Count, status);
                return orders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders by status: {Status}", status);
                throw;
            }
        }

        public async Task<List<OmnichannelOrder>> GetActiveOrdersAsync(string userId)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("Getting active orders for user: {UserId}", userId);

                string[] activeStatuses = ["pending", "confirmed", "preparing", "ready", "outfordelivery"];
                List<OmnichannelOrder> activeOrders =
                [
                    .. _orderStorage.Values
                                        .Where(order => activeStatuses.Contains(order.Status.Value))
                                        .Where(order => order.DeviceTracking.Any(dt => dt.IsActive))
                                        .OrderByDescending(order => order.CreatedAt)
,
                ];

                _logger.LogInformation("Found {OrderCount} active orders for user: {UserId}", activeOrders.Count, userId);
                return activeOrders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active orders for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<OrderCancellationResult> CancelOrderAsync(Guid orderId, string userId, string deviceId, string? reason = null)
        {
            try
            {
                _logger.LogInformation("Cancelling order: {OrderId} by user: {UserId}, reason: {Reason}", orderId, userId, reason);

                if (!_orderStorage.TryGetValue(orderId, out OmnichannelOrder? order))
                {
                    return new OrderCancellationResult
                    {
                        Success = false,
                        OrderId = orderId,
                        Errors = ["Order not found"]
                    };
                }

                OrderStatusId previousStatus = order.Status;
                OrderStatusId cancelledStatus = new("cancelled");

                // Check if order can be cancelled
                if (!CanCancelOrder(order))
                {
                    return new OrderCancellationResult
                    {
                        Success = false,
                        OrderId = orderId,
                        PreviousStatus = previousStatus,
                        NewStatus = previousStatus,
                        Errors = ["Order cannot be cancelled in current status"],
                        RequiresManualIntervention = true
                    };
                }

                OmnichannelOrder updatedOrder = order with
                {
                    Status = cancelledStatus,
                    StatusDescription = "Order cancelled",
                    UpdatedAt = DateTime.UtcNow,
                    LastUpdatedBy = userId,
                    LastUpdatedByDevice = deviceId,
                    IsSyncedAcrossDevices = false
                };

                // Update storage
                _orderStorage[orderId] = updatedOrder;

                // Calculate refund amount
                decimal? refundAmount = CalculateRefundAmount(order);

                // Sync across devices
                List<string> syncedDevices = await SyncOrderStatusAcrossDevicesAsync(orderId, cancelledStatus);

                // Create history entry
                await CreateOrderHistoryEntryAsync(orderId, OrderHistoryAction.Cancelled,
                    "Order cancelled", userId, deviceId, new { RefundAmount = refundAmount });

                // Update cache
                string cacheKey = $"order_{orderId}";
                _ = _cache.Set(cacheKey, updatedOrder, TimeSpan.FromHours(24));

                OrderCancellationResult result = new()
                {
                    Success = true,
                    OrderId = orderId,
                    PreviousStatus = previousStatus,
                    NewStatus = cancelledStatus,
                    CancelledAt = DateTime.UtcNow,
                    CancelledBy = userId,
                    CancelledByDevice = deviceId,
                    Reason = reason,
                    RefundAmount = refundAmount,
                    SyncedDevices = syncedDevices,
                    RequiresManualIntervention = false
                };

                _logger.LogInformation("Order cancelled successfully: {OrderId}", orderId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<OrderAnalytics> GetOrderAnalyticsAsync(string userId, DateTime? from = null, DateTime? to = null)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("Getting order analytics for user: {UserId} from {From} to {To}", userId, from, to);

                DateTime fromDate = from ?? DateTime.UtcNow.AddDays(-30);
                DateTime toDate = to ?? DateTime.UtcNow;

                List<OmnichannelOrder> userOrders = _orderStorage.Values
                    .Where(order => order.CreatedBy == userId)
                    .Where(order => order.CreatedAt >= fromDate && order.CreatedAt <= toDate)
                    .ToList();

                OrderAnalytics analytics = new()
                {
                    UserId = userId,
                    PeriodStart = fromDate,
                    PeriodEnd = toDate,
                    TotalOrders = userOrders.Count,
                    CompletedOrders = userOrders.Count(o => o.Status.Value == "completed"),
                    CancelledOrders = userOrders.Count(o => o.Status.Value == "cancelled"),
                    PendingOrders = userOrders.Count(o => o.Status.Value is "pending" or "confirmed"),
                    TotalRevenue = userOrders.Where(o => o.Status.Value == "completed").Sum(o => o.TotalAmount),
                    AverageOrderValue = userOrders.Count > 0 ? userOrders.Average(o => o.TotalAmount) : 0,
                    OrdersByStatus = userOrders.GroupBy(o => o.Status)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    OrdersByDevice = userOrders
                        .SelectMany(o => o.DeviceTracking)
                        .GroupBy(dt => dt.DeviceId)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    RevenueByDevice = userOrders
                        .SelectMany(o => o.DeviceTracking.Select(dt => new { dt.DeviceId, o.TotalAmount }))
                        .GroupBy(x => x.DeviceId)
                        .ToDictionary(g => g.Key, g => g.Sum(x => x.TotalAmount)),
                    TopProducts = userOrders
                        .SelectMany(o => o.Items)
                        .GroupBy(item => item.ProductId)
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .Select(g => g.Key.ToString())
                        .ToList(),
                    TopCategories = userOrders
                        .SelectMany(o => o.Items)
                        .GroupBy(item => GetProductCategory(item.ProductId))
                        .OrderByDescending(g => g.Count())
                        .Take(3)
                        .Select(g => g.Key)
                        .ToList()
                };

                _logger.LogInformation("Order analytics generated for user: {UserId}, TotalOrders: {TotalOrders}",
                    userId, analytics.TotalOrders);

                return analytics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order analytics for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<List<OrderConflict>> DetectOrderConflictsAsync(Guid orderId, string userId)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("Detecting conflicts for order: {OrderId}", orderId);

                if (!_orderStorage.TryGetValue(orderId, out OmnichannelOrder? order))
                {
                    return [];
                }

                List<OrderConflict> conflicts = [];

                // Simulate conflict detection based on device tracking
                List<IGrouping<string, OrderDeviceTracking>> deviceGroups = order.DeviceTracking
                    .GroupBy(dt => dt.DeviceId)
                    .ToList();

                if (deviceGroups.Count > 1)
                {
                    // Check for potential conflicts between devices
                    foreach (IGrouping<string, OrderDeviceTracking>? group1 in deviceGroups)
                    {
                        foreach (IGrouping<string, OrderDeviceTracking>? group2 in deviceGroups.Skip(deviceGroups.IndexOf(group1) + 1))
                        {
                            OrderConflict? conflict = DetectConflictBetweenDevices(order, group1.Key, group2.Key);
                            if (conflict != null)
                            {
                                conflicts.Add(conflict);
                            }
                        }
                    }
                }

                // Store conflicts
                _orderConflicts[orderId] = conflicts;

                _logger.LogInformation("Detected {ConflictCount} conflicts for order: {OrderId}", conflicts.Count, orderId);
                return conflicts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting conflicts for order: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<OrderConflictResolution> ResolveOrderConflictAsync(Guid orderId, string conflictId, OrderConflictResolutionStrategy strategy, string userId)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("Resolving conflict {ConflictId} for order: {OrderId} using strategy: {Strategy}",
                    conflictId, orderId, strategy);

                if (!_orderConflicts.TryGetValue(orderId, out List<OrderConflict>? conflicts))
                {
                    return new OrderConflictResolution
                    {
                        ConflictId = conflictId,
                        OrderId = orderId,
                        Strategy = strategy,
                        Success = false,
                        ResolvedBy = userId,
                        ResolvedAt = DateTime.UtcNow,
                        ResolutionDescription = "Conflict not found"
                    };
                }

                OrderConflict? conflict = conflicts.FirstOrDefault(c => c.ConflictId == conflictId);
                if (conflict == null)
                {
                    return new OrderConflictResolution
                    {
                        ConflictId = conflictId,
                        OrderId = orderId,
                        Strategy = strategy,
                        Success = false,
                        ResolvedBy = userId,
                        ResolvedAt = DateTime.UtcNow,
                        ResolutionDescription = "Conflict not found"
                    };
                }

                object? resolvedValue = ApplyConflictResolutionStrategy(conflict, strategy);
                List<string> affectedDevices = [conflict.LocalDeviceId, conflict.RemoteDeviceId];

                // Remove resolved conflict
                _ = conflicts.Remove(conflict);

                // Create history entry
                await CreateOrderHistoryEntryAsync(orderId, OrderHistoryAction.ConflictResolved,
                    $"Conflict resolved using {strategy}", userId, "system",
                    new { conflict.ConflictId, Strategy = strategy, ResolvedValue = resolvedValue });

                OrderConflictResolution result = new()
                {
                    ConflictId = conflictId,
                    OrderId = orderId,
                    Strategy = strategy,
                    ResolvedValue = resolvedValue,
                    Success = true,
                    ResolvedBy = userId,
                    ResolvedByDevice = "system",
                    ResolvedAt = DateTime.UtcNow,
                    ResolutionDescription = $"Conflict resolved using {strategy} strategy",
                    AffectedDevices = affectedDevices
                };

                _logger.LogInformation("Conflict resolved successfully: {ConflictId}", conflictId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving conflict: {ConflictId}", conflictId);
                throw;
            }
        }

        public async Task<OrderWorkflowStatus> GetOrderWorkflowStatusAsync(Guid orderId, string userId)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("Getting workflow status for order: {OrderId}", orderId);

                if (!_orderStorage.TryGetValue(orderId, out OmnichannelOrder? order))
                {
                    throw new InvalidOperationException($"Order {orderId} not found");
                }

                OrderWorkflowStatus workflowStatus = new()
                {
                    OrderId = orderId,
                    CurrentStep = order.WorkflowInfo.CurrentStep,
                    CompletedSteps = GetWorkflowSteps(order.WorkflowInfo.CompletedSteps),
                    PendingSteps = GetWorkflowSteps(order.WorkflowInfo.PendingSteps),
                    CurrentStepInfo = GetWorkflowStepInfo(order.WorkflowInfo.CurrentStep),
                    WorkflowStartedAt = order.WorkflowInfo.WorkflowStartedAt,
                    WorkflowCompletedAt = order.WorkflowInfo.WorkflowCompletedAt,
                    IsWorkflowComplete = order.WorkflowInfo.IsWorkflowComplete,
                    WorkflowProgress = CalculateWorkflowProgress(order.WorkflowInfo),
                    AvailableActions = GetAvailableActions(order.WorkflowInfo.CurrentStep)
                };

                _logger.LogInformation("Workflow status retrieved for order: {OrderId}, CurrentStep: {CurrentStep}",
                    orderId, workflowStatus.CurrentStep);

                return workflowStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow status for order: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<WorkflowAdvanceResult> AdvanceOrderWorkflowAsync(Guid orderId, string userId, string deviceId, Dictionary<string, object>? parameters = null)
        {
            try
            {
                _logger.LogInformation("Advancing workflow for order: {OrderId} by user: {UserId}", orderId, userId);

                if (!_orderStorage.TryGetValue(orderId, out OmnichannelOrder? order))
                {
                    return new WorkflowAdvanceResult
                    {
                        Success = false,
                        OrderId = orderId,
                        Errors = ["Order not found"]
                    };
                }

                string currentStep = order.WorkflowInfo.CurrentStep;
                string nextStep = GetNextWorkflowStep(currentStep);

                if (string.IsNullOrEmpty(nextStep))
                {
                    return new WorkflowAdvanceResult
                    {
                        Success = false,
                        OrderId = orderId,
                        PreviousStep = currentStep,
                        Errors = ["No next step available"]
                    };
                }

                // Update workflow info
                OrderWorkflowInfo updatedWorkflowInfo = order.WorkflowInfo with
                {
                    CurrentStep = nextStep,
                    CompletedSteps = [.. order.WorkflowInfo.CompletedSteps, currentStep],
                    PendingSteps = order.WorkflowInfo.PendingSteps.Skip(1).ToList(),
                    WorkflowData = parameters ?? []
                };

                OmnichannelOrder updatedOrder = order with
                {
                    WorkflowInfo = updatedWorkflowInfo,
                    UpdatedAt = DateTime.UtcNow,
                    LastUpdatedBy = userId,
                    LastUpdatedByDevice = deviceId
                };

                // Update storage
                _orderStorage[orderId] = updatedOrder;

                // Sync across devices
                List<string> syncedDevices = await SyncOrderStatusAcrossDevicesAsync(orderId, order.Status);

                // Create history entry
                await CreateOrderHistoryEntryAsync(orderId, OrderHistoryAction.WorkflowAdvanced,
                    $"Workflow advanced from {currentStep} to {nextStep}", userId, deviceId, parameters);

                // Update cache
                string cacheKey = $"order_{orderId}";
                _ = _cache.Set(cacheKey, updatedOrder, TimeSpan.FromHours(24));

                WorkflowAdvanceResult result = new()
                {
                    Success = true,
                    OrderId = orderId,
                    PreviousStep = currentStep,
                    NewStep = nextStep,
                    AdvancedAt = DateTime.UtcNow,
                    AdvancedBy = userId,
                    AdvancedByDevice = deviceId,
                    SyncedDevices = syncedDevices,
                    WorkflowData = parameters ?? []
                };

                _logger.LogInformation("Workflow advanced successfully for order: {OrderId} from {PreviousStep} to {NewStep}",
                    orderId, currentStep, nextStep);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error advancing workflow for order: {OrderId}", orderId);
                throw;
            }
        }

        #region Private Helper Methods

        private async Task CreateOrderHistoryEntryAsync(Guid orderId, OrderHistoryAction action, string description, string userId, string deviceId, object? data = null)
        {
            await Task.CompletedTask;
            string historyKey = $"history_{orderId}";
            if (!_orderHistory.TryGetValue(historyKey, out List<OrderHistoryEntry>? history))
            {
                history = [];
                _orderHistory[historyKey] = history;
            }

            OrderHistoryEntry entry = new()
            {
                OrderId = orderId,
                Action = action,
                Description = description,
                UserId = userId,
                DeviceId = deviceId,
                Timestamp = DateTime.UtcNow,
                NewValue = data
            };

            history.Add(entry);
        }

        private async Task UpdateDeviceTrackingAsync(Guid orderId, string userId, string action)
        {
            if (!_orderStorage.TryGetValue(orderId, out OmnichannelOrder? order))
            {
                return;
            }

            string deviceId = GetDeviceIdForUser(userId);
            if (string.IsNullOrEmpty(deviceId))
            {
                return;
            }

            OrderDeviceTracking? existingTracking = order.DeviceTracking.FirstOrDefault(dt => dt.DeviceId == deviceId);
            if (existingTracking != null)
            {
                // Update existing tracking
                OrderDeviceTracking updatedTracking = existingTracking with
                {
                    LastAccessedAt = DateTime.UtcNow,
                    LastAction = action,
                    IsActive = true
                };

                List<OrderDeviceTracking> updatedDeviceTracking = order.DeviceTracking.Select(dt =>
                    dt.DeviceId == deviceId ? updatedTracking : dt).ToList();

                OmnichannelOrder updatedOrder = order with { DeviceTracking = updatedDeviceTracking };
                _orderStorage[orderId] = updatedOrder;
            }
            else
            {
                // Add new tracking
                OrderDeviceTracking newTracking = new()
                {
                    DeviceId = deviceId,
                    DeviceType = DetermineDeviceType(deviceId),
                    FirstAccessedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow,
                    LastAction = action,
                    IsActive = true
                };

                List<OrderDeviceTracking> updatedDeviceTracking = [.. order.DeviceTracking, newTracking];
                OmnichannelOrder updatedOrder = order with { DeviceTracking = updatedDeviceTracking };
                _orderStorage[orderId] = updatedOrder;
            }
        }

        private async Task UpdateWorkflowInfoAsync(Guid orderId, OrderStatusId status)
        {
            if (!_orderStorage.TryGetValue(orderId, out OmnichannelOrder? order))
            {
                return;
            }

            string workflowStep = MapStatusToWorkflowStep(status.Value);
            if (string.IsNullOrEmpty(workflowStep))
            {
                return;
            }

            OrderWorkflowInfo updatedWorkflowInfo = order.WorkflowInfo with
            {
                CurrentStep = workflowStep,
                CompletedSteps = order.WorkflowInfo.CompletedSteps.Contains(workflowStep)
                    ? order.WorkflowInfo.CompletedSteps
                    : [.. order.WorkflowInfo.CompletedSteps, workflowStep],
                PendingSteps = order.WorkflowInfo.PendingSteps.Where(step => step != workflowStep).ToList(),
                IsWorkflowComplete = status.Value is "completed" or "cancelled"
            };

            OmnichannelOrder updatedOrder = order with { WorkflowInfo = updatedWorkflowInfo };
            _orderStorage[orderId] = updatedOrder;
        }

        private async Task<List<string>> SyncOrderStatusAcrossDevicesAsync(Guid orderId, OrderStatusId status)
        {
            if (!_orderStorage.TryGetValue(orderId, out OmnichannelOrder? order))
            {
                return [];
            }

            List<string> syncedDevices = [];
            foreach (OrderDeviceTracking? deviceTracking in order.DeviceTracking.Where(dt => dt.IsActive))
            {
                try
                {
                    // Simulate sync to device
                    await Task.Delay(Random.Shared.Next(5, 25));
                    syncedDevices.Add(deviceTracking.DeviceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync status to device: {DeviceId}", deviceTracking.DeviceId);
                }
            }

            return syncedDevices;
        }

        private static string DetermineDeviceType(string deviceId)
        {
            return deviceId.ToLower(CultureInfo.InvariantCulture) switch
            {
                var s when s.Contains("mobile") => "Mobile",
                var s when s.Contains("tablet") => "Tablet",
                var s when s.Contains("desktop") => "Desktop",
                var s when s.Contains("watch") => "Wearable",
                _ => "Unknown"
            };
        }

        private static string GetStatusDescription(OrderStatusId status)
        {
            return status.Value switch
            {
                "pending" => "Pending confirmation",
                "confirmed" => "Order confirmed",
                "preparing" => "Order being prepared",
                "ready" => "Ready for pickup",
                "outfordelivery" => "Out for delivery",
                "delivered" => "Order delivered",
                "completed" => "Order completed",
                "cancelled" => "Order cancelled",
                _ => "Unknown status"
            };
        }

        private static bool CanCancelOrder(OmnichannelOrder order)
        {
            string[] cancellableStatuses = ["pending", "confirmed"];
            return cancellableStatuses.Contains(order.Status.Value);
        }

        private static decimal? CalculateRefundAmount(OmnichannelOrder order)
        {
            // Simple refund logic - full refund for cancelled orders
            return order.Status.Value == "cancelled" ? order.TotalAmount : null;
        }

        private static string GenerateSyncVersion()
        {
            return $"v{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        }

        private static string GetDeviceIdForUser(string userId)
        {
            // Simple mapping - in production, this would be more sophisticated
            return $"device_{userId}";
        }

        private static OrderConflict? DetectConflictBetweenDevices(OmnichannelOrder order, string device1, string device2)
        {
            // Simple conflict detection - in production, this would be more sophisticated
            return order.DeviceTracking.Count(dt => dt.IsActive) > 1
                ? new OrderConflict
                {
                    OrderId = order.OrderId,
                    ConflictType = "ConcurrentAccess",
                    PropertyName = "OrderAccess",
                    LocalDeviceId = device1,
                    RemoteDeviceId = device2,
                    LocalTimestamp = DateTime.UtcNow.AddMinutes(-1),
                    RemoteTimestamp = DateTime.UtcNow,
                    Severity = OrderConflictSeverity.Low,
                    Description = "Multiple devices accessing order simultaneously",
                    RequiresUserIntervention = false
                }
                : null;
        }

        private static object? ApplyConflictResolutionStrategy(OrderConflict conflict, OrderConflictResolutionStrategy strategy)
        {
            return strategy switch
            {
                OrderConflictResolutionStrategy.LocalWins => conflict.LocalValue,
                OrderConflictResolutionStrategy.RemoteWins => conflict.RemoteValue,
                OrderConflictResolutionStrategy.LastWriteWins => conflict.RemoteTimestamp > conflict.LocalTimestamp ? conflict.RemoteValue : conflict.LocalValue,
                OrderConflictResolutionStrategy.Merge => throw new NotImplementedException(),
                OrderConflictResolutionStrategy.UserChoice => throw new NotImplementedException(),
                OrderConflictResolutionStrategy.Skip => throw new NotImplementedException(),
                OrderConflictResolutionStrategy.CreateConflict => throw new NotImplementedException(),
                _ => conflict.RemoteValue
            };
        }

        private static List<WorkflowStep> GetWorkflowSteps(List<string> stepIds)
        {
            return stepIds.Select(GetWorkflowStepInfo)
                         .Where(step => step != null)
                         .Cast<WorkflowStep>()
                         .ToList();
        }

        private static WorkflowStep? GetWorkflowStepInfo(string stepId)
        {
            return stepId switch
            {
                "PENDING" => new WorkflowStep { StepId = "PENDING", StepName = "Pending", StepDescription = "Order pending confirmation", IsRequired = true },
                "CONFIRMED" => new WorkflowStep { StepId = "CONFIRMED", StepName = "Confirmed", StepDescription = "Order confirmed", IsRequired = true },
                "PREPARING" => new WorkflowStep { StepId = "PREPARING", StepName = "Preparing", StepDescription = "Order being prepared", IsRequired = true },
                "READY" => new WorkflowStep { StepId = "READY", StepName = "Ready", StepDescription = "Order ready for pickup", IsRequired = true },
                "DELIVERED" => new WorkflowStep { StepId = "DELIVERED", StepName = "Delivered", StepDescription = "Order delivered", IsRequired = true },
                _ => null
            };
        }

        private static decimal CalculateWorkflowProgress(OrderWorkflowInfo workflowInfo)
        {
            int totalSteps = workflowInfo.CompletedSteps.Count + workflowInfo.PendingSteps.Count;
            return totalSteps > 0 ? (decimal)workflowInfo.CompletedSteps.Count / totalSteps : 0;
        }

        private static List<string> GetAvailableActions(string currentStep)
        {
            return currentStep switch
            {
                "PENDING" => ["CONFIRM", "CANCEL"],
                "CONFIRMED" => ["START_PREPARING", "CANCEL"],
                "PREPARING" => ["MARK_READY", "CANCEL"],
                "READY" => ["MARK_DELIVERED", "COMPLETE"],
                "DELIVERED" => ["COMPLETE"],
                _ => []
            };
        }

        private static string GetNextWorkflowStep(string currentStep)
        {
            return currentStep switch
            {
                "PENDING" => "CONFIRMED",
                "CONFIRMED" => "PREPARING",
                "PREPARING" => "READY",
                "READY" => "DELIVERED",
                "DELIVERED" => "COMPLETED",
                _ => string.Empty
            };
        }

        private static string MapStatusToWorkflowStep(string status)
        {
            return status switch
            {
                "pending" => "PENDING",
                "confirmed" => "CONFIRMED",
                "preparing" => "PREPARING",
                "ready" => "READY",
                "outfordelivery" => "DELIVERED",
                "delivered" => "DELIVERED",
                "completed" => "COMPLETED",
                "cancelled" => "CANCELLED",
                _ => string.Empty
            };
        }

        private static string GetProductCategory(Guid productId)
        {
            // Simple category mapping - in production, this would be more sophisticated
            return productId.ToString().StartsWith("1", StringComparison.OrdinalIgnoreCase) ? "Đồ uống" : "Đồ ăn";
        }

        #endregion
    }
}
