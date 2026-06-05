using VanAn.Shared.Domain;
using VanAn.KhachLink.Models;

namespace VanAn.KhachLink.Services
{
    public interface IConflictResolutionService
    {
        Task<ResolutionResult> ResolveOrderConflictAsync(OfflineOrderDto offlineOrder, Order? serverOrder);
        Task<ResolutionResult> ResolveCartConflictAsync(List<OfflineOrderItemDto> offlineItems, List<CartItem> serverItems);
        Task<bool> ValidateOrderAsync(OfflineOrderDto order);
        Task<bool> ValidateCartAsync(List<OfflineOrderItemDto> items);
        Task<ConflictReport> GenerateConflictReportAsync(OfflineOrderDto offlineOrder, Order? serverOrder);
    }

    public class ResolutionResult
    {
        public bool Success { get; set; }
        public ResolutionAction Action { get; set; }
        public string? Reason { get; set; }
        public OfflineOrderDto? MergedOrder { get; set; }
        public List<OfflineOrderItemDto>? MergedItems { get; set; }
        public List<string> Warnings { get; set; } = [];
    }

    public class ConflictReport
    {
        public string OrderId { get; set; } = string.Empty;
        public bool HasConflict { get; set; }
        public List<string> Conflicts { get; set; } = [];
        public List<string> Warnings { get; set; } = [];
        public ResolutionAction RecommendedAction { get; set; }
        public string Recommendation { get; set; } = string.Empty;
    }

    public class ConflictResolutionService(
        ILogger<ConflictResolutionService> logger,
        ISyncConflictResolver baseResolver) : IConflictResolutionService
    {
        private readonly ILogger<ConflictResolutionService> _logger = logger;
        private readonly ISyncConflictResolver _baseResolver = baseResolver;

        public async Task<ResolutionResult> ResolveOrderConflictAsync(OfflineOrderDto offlineOrder, Order? serverOrder)
        {
            ResolutionResult result = new();

            try
            {
                // Validate offline order first
                if (!await ValidateOrderAsync(offlineOrder))
                {
                    result.Success = false;
                    result.Action = ResolutionAction.Error;
                    result.Reason = "Offline order validation failed";
                    return result;
                }

                // Use base resolver for initial resolution
                ConflictResolution baseResolution = await _baseResolver.ResolveOrderConflictAsync(offlineOrder, serverOrder);

                if (!baseResolution.Success)
                {
                    result.Success = false;
                    result.Action = ResolutionAction.Error;
                    result.Reason = baseResolution.Reason;
                    return result;
                }

                // Apply enhanced logic based on action
                switch (baseResolution.Action)
                {
                    case ResolutionAction.UseOffline:
                        result = await ProcessUseOfflineAsync(offlineOrder, serverOrder);
                        break;

                    case ResolutionAction.UseServer:
                        result = await ProcessUseServerAsync(offlineOrder, serverOrder);
                        break;

                    case ResolutionAction.Merge:
                        result = await ProcessMergeAsync(offlineOrder, serverOrder);
                        break;
                    case ResolutionAction.Skip:
                        break;
                    case ResolutionAction.Error:
                        break;
                    default:
                        result.Success = false;
                        result.Action = ResolutionAction.Error;
                        result.Reason = "Unknown resolution action";
                        break;
                }

                _logger.LogInformation("Order conflict resolved: {OrderId}, Action: {Action}",
                    offlineOrder.Id, result.Action);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Action = ResolutionAction.Error;
                result.Reason = ex.Message;
                _logger.LogError(ex, "Failed to resolve order conflict: {OrderId}", offlineOrder.Id);
            }

            return result;
        }

        public async Task<ResolutionResult> ResolveCartConflictAsync(List<OfflineOrderItemDto> offlineItems, List<CartItem> serverItems)
        {
            ResolutionResult result = new();

            try
            {
                // Validate cart items
                if (!await ValidateCartAsync(offlineItems))
                {
                    result.Success = false;
                    result.Action = ResolutionAction.Error;
                    result.Reason = "Offline cart validation failed";
                    return result;
                }

                // Use base resolver
                ConflictResolution baseResolution = await _baseResolver.ResolveCartConflictAsync(offlineItems, serverItems);

                if (!baseResolution.Success)
                {
                    result.Success = false;
                    result.Action = ResolutionAction.Error;
                    result.Reason = baseResolution.Reason;
                    return result;
                }

                // Process merged items
                if (baseResolution.MergedOrder != null)
                {
                    result.MergedItems = baseResolution.MergedOrder.Items;
                    result.Action = ResolutionAction.Merge;
                    result.Success = true;
                    result.Reason = baseResolution.Reason;

                    // Add warnings for potential issues
                    await CheckCartWarningsAsync(result.MergedItems, result);
                }

                _logger.LogInformation("Cart conflict resolved with {ItemCount} items",
                    result.MergedItems?.Count ?? 0);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Action = ResolutionAction.Error;
                result.Reason = ex.Message;
                _logger.LogError(ex, "Failed to resolve cart conflict");
            }

            return result;
        }

        public async Task<bool> ValidateOrderAsync(OfflineOrderDto order)
        {
            try
            {
                // Basic validation
                if (string.IsNullOrEmpty(order.Id) || string.IsNullOrEmpty(order.CustomerId))
                {
                    return false;
                }

                if (order.Items == null || order.Items.Count == 0)
                {
                    return false;
                }

                // Validate items
                foreach (OfflineOrderItemDto item in order.Items)
                {
                    if (string.IsNullOrEmpty(item.ProductId) || item.Quantity <= 0 || item.UnitPrice <= 0)
                    {
                        return false;
                    }

                    if (Math.Abs(item.TotalPrice - (item.Quantity * item.UnitPrice)) > 0.01m)
                    {
                        return false;
                    }
                }

                // Validate total amount
                decimal calculatedTotal = order.Items.Sum(i => i.TotalPrice);
                return Math.Abs(calculatedTotal - order.TotalAmount) <= 0.01m;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate order: {OrderId}", order.Id);
                return false;
            }
        }

        public async Task<bool> ValidateCartAsync(List<OfflineOrderItemDto> items)
        {
            try
            {
                if (items == null || items.Count == 0)
                {
                    return true; // Empty cart is valid
                }

                foreach (OfflineOrderItemDto item in items)
                {
                    if (string.IsNullOrEmpty(item.ProductId) || item.Quantity <= 0 || item.UnitPrice <= 0)
                    {
                        return false;
                    }

                    if (Math.Abs(item.TotalPrice - (item.Quantity * item.UnitPrice)) > 0.01m)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate cart items");
                return false;
            }
        }

        public async Task<ConflictReport> GenerateConflictReportAsync(OfflineOrderDto offlineOrder, Order? serverOrder)
        {
            ConflictReport report = new()
            {
                OrderId = offlineOrder.Id,
                HasConflict = false
            };

            try
            {
                if (serverOrder == null)
                {
                    report.RecommendedAction = ResolutionAction.UseOffline;
                    report.Recommendation = "No server order found - use offline order";
                    return report;
                }

                // Check for conflicts
                List<string> conflicts = [];
                List<string> warnings = [];

                // Timestamp conflict
                if (Math.Abs((offlineOrder.CreatedAt - serverOrder.CreatedAt).TotalMinutes) > 5)
                {
                    conflicts.Add($"Timestamp mismatch: Offline {offlineOrder.CreatedAt}, Server {serverOrder.CreatedAt}");
                    report.HasConflict = true;
                }

                // Total amount conflict
                if (Math.Abs(offlineOrder.TotalAmount - serverOrder.TotalAmount) > 0.01m)
                {
                    conflicts.Add($"Total amount mismatch: Offline {offlineOrder.TotalAmount}, Server {serverOrder.TotalAmount}");
                    report.HasConflict = true;
                }

                // Items conflict
                HashSet<string> offlineProductIds = offlineOrder.Items.Select(i => i.ProductId).ToHashSet();
                HashSet<string> serverProductIds = serverOrder.Items.Select(i => i.ProductId.ToString()).ToHashSet();

                if (!offlineProductIds.SetEquals(serverProductIds))
                {
                    conflicts.Add("Items differ between offline and server orders");
                    report.HasConflict = true;
                }

                // Status conflict
                if (offlineOrder.Status != serverOrder.Status.Value)
                {
                    warnings.Add($"Status differs: Offline {offlineOrder.Status}, Server {serverOrder.Status}");
                }

                report.Conflicts = conflicts;
                report.Warnings = warnings;

                // Recommend action
                if (report.HasConflict)
                {
                    if (offlineOrder.CreatedAt > serverOrder.CreatedAt)
                    {
                        report.RecommendedAction = ResolutionAction.UseOffline;
                        report.Recommendation = "Offline order is newer - prefer offline version";
                    }
                    else
                    {
                        report.RecommendedAction = ResolutionAction.Merge;
                        report.Recommendation = "Server order is newer - merge items to preserve data";
                    }
                }
                else
                {
                    report.RecommendedAction = ResolutionAction.UseServer;
                    report.Recommendation = "No significant conflicts - use server version";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate conflict report: {OrderId}", offlineOrder.Id);
                report.HasConflict = true;
                report.RecommendedAction = ResolutionAction.Error;
                report.Recommendation = ex.Message;
            }

            return report;
        }

        private static async Task<ResolutionResult> ProcessUseOfflineAsync(OfflineOrderDto offlineOrder, Order? serverOrder)
        {
            ResolutionResult result = new()
            {
                Success = true,
                Action = ResolutionAction.UseOffline,
                Reason = "Using offline order",
                MergedOrder = offlineOrder
            };

            // Add warnings if server order exists
            if (serverOrder != null)
            {
                if (serverOrder.CreatedAt > offlineOrder.CreatedAt)
                {
                    result.Warnings.Add("Server order is newer but using offline version");
                }

                if (serverOrder.Status.Value != offlineOrder.Status)
                {
                    result.Warnings.Add($"Status differs: Server {serverOrder.Status}, Offline {offlineOrder.Status}");
                }
            }

            return result;
        }

        private static async Task<ResolutionResult> ProcessUseServerAsync(OfflineOrderDto offlineOrder, Order? serverOrder)
        {
            ResolutionResult result = new()
            {
                Success = true,
                Action = ResolutionAction.UseServer,
                Reason = "Using server order"
            };

            if (serverOrder != null)
            {
                result.MergedOrder = OfflineOrderDto.FromDomain(serverOrder);

                if (offlineOrder.CreatedAt > serverOrder.CreatedAt)
                {
                    result.Warnings.Add("Offline order is newer but using server version");
                }
            }
            else
            {
                result.Success = false;
                result.Action = ResolutionAction.Error;
                result.Reason = "Server order not found";
            }

            return result;
        }

        private async Task<ResolutionResult> ProcessMergeAsync(OfflineOrderDto offlineOrder, Order? serverOrder)
        {
            ResolutionResult result = new()
            {
                Success = true,
                Action = ResolutionAction.Merge,
                Reason = "Merging orders"
            };

            if (serverOrder != null)
            {
                // Create merged order
                OfflineOrderDto mergedOrder = new()
                {
                    Id = serverOrder.Id.ToString(),
                    CustomerId = serverOrder.CustomerId?.ToString() ?? string.Empty,
                    ShopId = serverOrder.TenantId.Value.ToString(),
                    Status = serverOrder.Status.Value,
                    CreatedAtTimestamp = ((DateTimeOffset)serverOrder.CreatedAt).ToUnixTimeMilliseconds(),
                    Items = []
                };

                // Merge items
                HashSet<string> processedProductIds = [];

                // Add server items first
                foreach (OrderItem serverItem in serverOrder.Items)
                {
                    mergedOrder.Items.Add(new OfflineOrderItemDto
                    {
                        ProductId = serverItem.ProductId.ToString(),
                        Quantity = serverItem.Quantity,
                        UnitPrice = serverItem.UnitPrice,
                        TotalPrice = serverItem.TotalPrice
                    });
                    _ = processedProductIds.Add(serverItem.ProductId.ToString());
                }

                // Add offline items not in server
                foreach (OfflineOrderItemDto offlineItem in offlineOrder.Items)
                {
                    if (!processedProductIds.Contains(offlineItem.ProductId))
                    {
                        mergedOrder.Items.Add(offlineItem);
                    }
                }

                // Recalculate total
                mergedOrder.TotalAmount = mergedOrder.Items.Sum(i => i.TotalPrice);

                result.MergedOrder = mergedOrder;

                // Add warnings
                if (Math.Abs((offlineOrder.CreatedAt - serverOrder.CreatedAt).TotalMinutes) > 5)
                {
                    result.Warnings.Add("Merging orders with significant timestamp difference");
                }
            }
            else
            {
                result.Success = false;
                result.Action = ResolutionAction.Error;
                result.Reason = "Server order not found for merge";
            }

            return result;
        }

        private static async Task CheckCartWarningsAsync(List<OfflineOrderItemDto> items, ResolutionResult result)
        {
            if (items.Count > 20)
            {
                result.Warnings.Add("Large number of items in cart may affect performance");
            }

            decimal totalValue = items.Sum(i => i.TotalPrice);
            if (totalValue > 1000000) // 1 million VND
            {
                result.Warnings.Add("High-value cart - consider confirming with customer");
            }

            List<OfflineOrderItemDto> highQuantityItems = items.Where(i => i.Quantity > 10).ToList();
            if (highQuantityItems.Count != 0)
            {
                result.Warnings.Add($"Items with high quantity: {string.Join(", ", highQuantityItems.Select(i => i.ProductId))}");
            }
        }
    }
}
