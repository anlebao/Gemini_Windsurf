using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;
using VanAn.Shared.Services;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Kitchen service implementation with FIFO grouping and defensive validation
    /// </summary>
    public class KitchenService(VanAnDbContext context, ILogger<KitchenService> logger) : IKitchenService
    {
        private readonly VanAnDbContext _context = context;
        private readonly ILogger<KitchenService> _logger = logger;

        // 🎯 CORE FIFO GROUPING LOGIC - ARCHITECT'S HYBRID APPROACH
        public async Task<List<KitchenItemGroupDto>> GetGroupedKitchenItemsAsync(Guid shopId)
        {
            // 🛡️ STEP 1: SQL Projection - Server-side filtering & flat projection
            var flatItems = await _context.OrderItems
                .Where(oi => oi.Order.TenantId == new TenantId(shopId) &&
                            (oi.KitchenStatus == KitchenStatus.Pending || oi.KitchenStatus == KitchenStatus.Preparing))
                .Select(oi => new
                {
                    OrderItemId = oi.Id,
                    oi.ProductId,
                    ProductName = oi.Product.Name,
                    oi.Quantity,
                    Status = oi.KitchenStatus,
                    VoiceNoteText = oi.ItemNoteText ?? oi.Order.VoiceNoteText,
                    VoiceNoteAudioBlob = oi.ItemNoteAudioBlob ?? oi.Order.VoiceNoteAudioBlob,
                    OrderCreatedAt = oi.Order.OrderDate,
                    oi.OrderId
                })
                .ToListAsync();

            //  STEP 2: In-Memory Grouping - Safe client-side with bounded memory
            try
            {
                List<KitchenItemGroupDto> groupedItems =
                [
                    .. flatItems
                                        .GroupBy(item => new { item.ProductId, item.ProductName })
                                        .Select(g => new KitchenItemGroupDto
                                        {
                                            ProductId = g.Key.ProductId,
                                            ProductName = g.Key.ProductName,
                                            TotalQuantity = g.Sum(item => item.Quantity),
                                            GroupStatus = g.All(item => item.Status == KitchenStatus.Pending) ? KitchenStatus.Pending : KitchenStatus.Preparing,
                                            OldestOrderTime = g.Min(item => item.OrderCreatedAt),
                                            Items =
                                            // FIFO within group

                                            [
                                                .. g.Select(item => new GroupedOrderItemDto
                                                {
                                                    OrderItemId = item.OrderItemId,
                                                    OrderId = item.OrderId,
                                                    Quantity = item.Quantity,
                                                    Status = item.Status,
                                                    VoiceNoteText = item.VoiceNoteText,
                                                    VoiceNoteAudioBlob = item.VoiceNoteAudioBlob,
                                                    OrderCreatedAt = item.OrderCreatedAt
                                                }).OrderBy(item => item.OrderCreatedAt),
                                            ]
                                        })
                                        .OrderBy(g => g.OldestOrderTime)
, // FIFO between groups
                ];

                _logger.LogInformation("Retrieved {Count} grouped kitchen items for shop {ShopId}", groupedItems.Count, shopId);
                return groupedItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting kitchen items for shop {ShopId}", shopId);
                return [];
            }
        }

        // 📝 STATUS MANAGEMENT
        public async Task<bool> UpdateItemStatusAsync(KitchenStatusUpdateDto update, Guid userId)
        {
            OrderItem? orderItem = await _context.OrderItems
                .Include(oi => oi.Order)
                .FirstOrDefaultAsync(oi => oi.Id == update.OrderItemId);

            if (orderItem == null)
            {
                _logger.LogWarning("OrderItem {OrderItemId} not found", update.OrderItemId);
                return false;
            }

            KitchenStatus oldStatus = orderItem.KitchenStatus;
            orderItem.UpdateKitchenStatus(update.NewStatus);

            // Check if all items in the order are completed
            if (update.NewStatus == KitchenStatus.Completed)
            {
                List<OrderItem> remainingItems = await _context.OrderItems
                    .Where(oi => oi.OrderId == orderItem.OrderId && oi.Id != update.OrderItemId)
                    .ToListAsync();

                if (remainingItems.All(oi => oi.KitchenStatus == KitchenStatus.Completed))
                {
                    orderItem.Order.UpdateKitchenStatus(KitchenStatus.Completed);
                    orderItem.Order.MarkAsCompleted();
                }
            }

            _ = await _context.SaveChangesAsync();

            _logger.LogInformation("Updated OrderItem {OrderItemId} status from {OldStatus} to {NewStatus} by user {UserId}",
                update.OrderItemId, oldStatus, update.NewStatus, userId);

            return true;
        }

        // 🔍 ORDER TRACKING
        public async Task<KitchenStatus?> GetOrderKitchenStatusAsync(Guid orderId)
        {
            Order? order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            return order?.KitchenStatus;
        }

        public async Task<List<GroupedOrderItemDto>> GetOrderItemsAsync(Guid orderId)
        {
            List<GroupedOrderItemDto> orderItems = await _context.OrderItems
                .Include(oi => oi.Product)
                .Where(oi => oi.OrderId == orderId)
                .Select(oi => new GroupedOrderItemDto
                {
                    OrderItemId = oi.Id,
                    OrderId = oi.OrderId,
                    Quantity = oi.Quantity,
                    Status = oi.KitchenStatus,
                    VoiceNoteText = oi.ItemNoteText ?? oi.Order.VoiceNoteText,
                    VoiceNoteAudioBlob = oi.ItemNoteAudioBlob ?? oi.Order.VoiceNoteAudioBlob,
                    OrderCreatedAt = oi.Order.OrderDate
                })
                .ToListAsync();

            return orderItems;
        }

        // 📊 KITCHEN ANALYTICS
        public async Task<int> GetPendingItemsCountAsync(Guid shopId)
        {
            return await _context.OrderItems
                .Include(oi => oi.Order)
                .CountAsync(oi => oi.Order.TenantId == new TenantId(shopId) && oi.KitchenStatus == KitchenStatus.Pending);
        }

        public async Task<TimeSpan> GetAveragePreparationTimeAsync(Guid shopId, DateTime from)
        {
            List<OrderItem> completedItems = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.TenantId == new TenantId(shopId) &&
                            oi.KitchenStatus == KitchenStatus.Completed &&
                            oi.Order.CompletedAt.HasValue &&
                            oi.Order.OrderDate >= from)
                .ToListAsync();

            if (completedItems.Count == 0)
            {
                return TimeSpan.Zero;
            }

            double averageTicks = completedItems
                .Average(oi => (oi.Order.CompletedAt!.Value - oi.Order.OrderDate).Ticks);

            return TimeSpan.FromTicks((long)averageTicks);
        }

        // 🎤 VOICE NOTE HANDLING (CORRECTED SIGNATURE)
        public async Task<VoiceNoteDto> ProcessVoiceNoteAsync(Guid orderId, VoiceNoteDto inputDto)
        {
            // 🛡️ DEFENSIVE: Apply size constraints
            string? processedText = inputDto.Text;
            string? processedAudioBlob = inputDto.AudioBlob;
            bool transcriptionSuccessful = inputDto.TranscriptionSuccessful;

            // Text constraint: Max 500 characters
            if (!string.IsNullOrEmpty(processedText) && processedText.Length > 500)
            {
                processedText = processedText[..500];
                _logger.LogWarning("Voice note text truncated to 500 characters for order {OrderId}", orderId);
                transcriptionSuccessful = false; // Mark as failed due to truncation
            }

            // Audio constraint: Max 150KB Base64
            if (!string.IsNullOrEmpty(processedAudioBlob) && processedAudioBlob.Length > 150000)
            {
                processedAudioBlob = null; // Drop the audio blob
                _logger.LogWarning("Voice note audio blob dropped (exceeded 150KB) for order {OrderId}", orderId);
                transcriptionSuccessful = false; // Mark as failed due to size limit
            }

            // Update order with processed voice note
            Order? order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.UpdateVoiceNotes(processedText, processedAudioBlob);
                _ = await _context.SaveChangesAsync();
            }

            VoiceNoteDto result = new()
            {
                Text = processedText,
                AudioBlob = processedAudioBlob,
                TranscriptionSuccessful = transcriptionSuccessful,
                RecordedAt = inputDto.RecordedAt
            };

            _logger.LogInformation("Processed voice note for order {OrderId}: TextLength={TextLength}, HasAudio={HasAudio}, Success={Success}",
                orderId, processedText?.Length ?? 0, !string.IsNullOrEmpty(processedAudioBlob), transcriptionSuccessful);

            return result;
        }

        public async Task<bool> AttachVoiceNoteToItemAsync(Guid orderItemId, VoiceNoteDto voiceNote)
        {
            OrderItem? orderItem = await _context.OrderItems.FindAsync(orderItemId);
            if (orderItem == null)
            {
                _logger.LogWarning("OrderItem {OrderItemId} not found for voice note attachment", orderItemId);
                return false;
            }

            //  DEFENSIVE: Apply size constraints
            string? noteText = voiceNote.Text?.Length > 500 ? voiceNote.Text[..500] : voiceNote.Text;
            string? noteAudioBlob = voiceNote.AudioBlob?.Length > 150000 ? null : voiceNote.AudioBlob;
            orderItem.UpdateItemNotes(noteText, noteAudioBlob);

            _ = await _context.SaveChangesAsync();

            _logger.LogInformation("Attached voice note to OrderItem {OrderItemId}", orderItemId);
            return true;
        }

        public async Task<KitchenAnalyticsDto> GetKitchenAnalyticsAsync(Guid shopId, DateTime from)
        {
            List<Order> orders = await _context.Orders
                .Where(o => o.TenantId == new TenantId(shopId) && o.OrderDate >= from)
                .ToListAsync();

            List<Order> completedOrders = orders.Where(o => o.Status.Value == "Completed").ToList();
            List<Order> pendingOrders = orders.Where(o => o.Status.Value is "Pending" or "Preparing").ToList();

            return new KitchenAnalyticsDto
            {
                ShopId = shopId,
                PeriodStart = from,
                PeriodEnd = DateTime.UtcNow,
                TotalOrders = orders.Count,
                CompletedOrders = completedOrders.Count,
                PendingOrders = pendingOrders.Count,
                AveragePreparationTime = completedOrders.Count > 0
                    ? (double)completedOrders.Average(o => (o.CompletedAt - o.OrderDate)?.TotalMinutes ?? 0)
                    : 0,
                Performance = []
            };
        }
    }
}
