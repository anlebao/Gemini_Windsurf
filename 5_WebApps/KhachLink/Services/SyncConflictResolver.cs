using VanAn.Shared.Domain;
using VanAn.KhachLink.Models;

namespace VanAn.KhachLink.Services
{
    public interface ISyncConflictResolver
    {
        Task<ConflictResolution> ResolveOrderConflictAsync(OfflineOrderDto offlineOrder, Order? serverOrder);
        Task<ConflictResolution> ResolveCartConflictAsync(List<OfflineOrderItemDto> offlineItems, List<CartItem> serverItems);
        Task<bool> ValidateDataIntegrityAsync(OfflineOrderDto order);
    }

    public class ConflictResolution
    {
        public ResolutionAction Action { get; set; }
        public string? Reason { get; set; }
        public OfflineOrderDto? MergedOrder { get; set; }
        public bool Success => Action != ResolutionAction.Error;
    }

    public enum ResolutionAction
    {
        UseOffline,
        UseServer,
        Merge,
        Skip,
        Error
    }

    public class SyncConflictResolver(ILogger<SyncConflictResolver> logger) : ISyncConflictResolver
    {
        private readonly ILogger<SyncConflictResolver> _logger = logger;

        public async Task<ConflictResolution> ResolveOrderConflictAsync(OfflineOrderDto offlineOrder, Order? serverOrder)
        {
            try
            {
                // Case 1: No server order exists - use offline
                if (serverOrder == null)
                {
                    return new ConflictResolution
                    {
                        Action = ResolutionAction.UseOffline,
                        Reason = "No server order found"
                    };
                }

                // Case 2: Server order is newer - use server
                if (serverOrder.CreatedAt > offlineOrder.CreatedAt)
                {
                    return new ConflictResolution
                    {
                        Action = ResolutionAction.UseServer,
                        Reason = "Server order is newer"
                    };
                }

                // Case 3: Same timestamp - merge items
                if (Math.Abs((serverOrder.CreatedAt - offlineOrder.CreatedAt).TotalSeconds) < 5)
                {
                    OfflineOrderDto mergedOrder = await MergeOrdersAsync(offlineOrder, serverOrder);
                    return new ConflictResolution
                    {
                        Action = ResolutionAction.Merge,
                        Reason = "Orders created at same time - merging items",
                        MergedOrder = mergedOrder
                    };
                }

                // Case 4: Offline order is newer - use offline
                return new ConflictResolution
                {
                    Action = ResolutionAction.UseOffline,
                    Reason = "Offline order is newer"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve order conflict for offline order: {OrderId}", offlineOrder.Id);
                return new ConflictResolution
                {
                    Action = ResolutionAction.Error,
                    Reason = ex.Message
                };
            }
        }

        public async Task<ConflictResolution> ResolveCartConflictAsync(List<OfflineOrderItemDto> offlineItems, List<CartItem> serverItems)
        {
            try
            {
                // Merge cart items - combine unique items
                List<OfflineOrderItemDto> mergedItems = [];
                HashSet<string> processedProductIds = [];

                // Add offline items first
                foreach (OfflineOrderItemDto offlineItem in offlineItems)
                {
                    mergedItems.Add(offlineItem);
                    _ = processedProductIds.Add(offlineItem.ProductId);
                }

                // Add server items that aren't in offline
                foreach (CartItem serverItem in serverItems)
                {
                    if (!processedProductIds.Contains(serverItem.ProductId.ToString()))
                    {
                        mergedItems.Add(new OfflineOrderItemDto
                        {
                            ProductId = serverItem.ProductId.ToString(),
                            Quantity = serverItem.Quantity,
                            UnitPrice = serverItem.UnitPrice,
                            TotalPrice = serverItem.TotalPrice
                        });
                    }
                }

                return new ConflictResolution
                {
                    Action = ResolutionAction.Merge,
                    Reason = "Cart items merged successfully",
                    MergedOrder = new OfflineOrderDto { Items = mergedItems }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve cart conflict");
                return new ConflictResolution
                {
                    Action = ResolutionAction.Error,
                    Reason = ex.Message
                };
            }
        }

        public async Task<bool> ValidateDataIntegrityAsync(OfflineOrderDto order)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrEmpty(order.Id) || string.IsNullOrEmpty(order.CustomerId) || string.IsNullOrEmpty(order.ShopId))
                {
                    return false;
                }

                // Validate items
                if (order.Items == null || order.Items.Count == 0)
                {
                    return false;
                }

                // Validate each item
                foreach (OfflineOrderItemDto item in order.Items)
                {
                    if (string.IsNullOrEmpty(item.ProductId) || item.Quantity <= 0 || item.UnitPrice <= 0)
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
                _logger.LogError(ex, "Failed to validate data integrity for order: {OrderId}", order.Id);
                return false;
            }
        }

        private async Task<OfflineOrderDto> MergeOrdersAsync(OfflineOrderDto offlineOrder, Order serverOrder)
        {
            // Create merged order with server data as base
            OfflineOrderDto mergedOrder = new()
            {
                Id = serverOrder.Id.ToString(),
                CustomerId = serverOrder.CustomerId?.ToString() ?? string.Empty,
                ShopId = serverOrder.TenantId.Value.ToString(),
                TotalAmount = serverOrder.TotalAmount,
                Status = serverOrder.Status.Value,
                CreatedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Items = []
            };

            // Merge items from both orders
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

            // Add offline items that aren't in server
            foreach (OfflineOrderItemDto offlineItem in offlineOrder.Items)
            {
                if (!processedProductIds.Contains(offlineItem.ProductId))
                {
                    mergedOrder.Items.Add(offlineItem);
                }
            }

            // Recalculate total
            mergedOrder.TotalAmount = mergedOrder.Items.Sum(i => i.TotalPrice);

            return mergedOrder;
        }
    }
}
