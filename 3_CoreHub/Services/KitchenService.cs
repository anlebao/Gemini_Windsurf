using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;
using VanAn.Shared.Services;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Kitchen service implementation with FIFO grouping and defensive validation
/// </summary>
public class KitchenService : IKitchenService
{
    private readonly VanAnDbContext _context;
    private readonly ILogger<KitchenService> _logger;

    public KitchenService(VanAnDbContext context, ILogger<KitchenService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // 🎯 CORE FIFO GROUPING LOGIC - ARCHITECT'S HYBRID APPROACH
    public async Task<List<KitchenItemGroupDto>> GetGroupedKitchenItemsAsync(Guid shopId)
    {
        // 🛡️ STEP 1: SQL Projection - Server-side filtering & flat projection
        var flatItems = await _context.OrderItems
            .Where(oi => oi.Order.TenantId.Value == shopId && 
                        (oi.KitchenStatus == KitchenStatus.Pending || oi.KitchenStatus == KitchenStatus.Preparing))
            .Select(oi => new {
                OrderItemId = oi.Id,
                ProductId = oi.ProductId,
                ProductName = oi.Product.Name,
                Quantity = oi.Quantity,
                Status = oi.KitchenStatus,
                VoiceNoteText = oi.ItemNoteText ?? oi.Order.VoiceNoteText,
                VoiceNoteAudioBlob = oi.ItemNoteAudioBlob ?? oi.Order.VoiceNoteAudioBlob,
                OrderCreatedAt = oi.Order.OrderDate,
                OrderId = oi.OrderId
            })
            .ToListAsync();

        //  STEP 2: In-Memory Grouping - Safe client-side with bounded memory
        try
        {
            var groupedItems = flatItems
                .GroupBy(item => new { item.ProductId, item.ProductName })
                .Select(g => new KitchenItemGroupDto
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    TotalQuantity = g.Sum(item => item.Quantity),
                GroupStatus = g.All(item => item.Status == KitchenStatus.Pending) ? KitchenStatus.Pending : KitchenStatus.Preparing,
                    OldestOrderTime = g.Min(item => item.OrderCreatedAt),
                    Items = g.Select(item => new GroupedOrderItemDto
                    {
                        OrderItemId = item.OrderItemId,
                        OrderId = item.OrderId,
                        Quantity = item.Quantity,
                        Status = item.Status,
                        VoiceNoteText = item.VoiceNoteText,
                        VoiceNoteAudioBlob = item.VoiceNoteAudioBlob,
                        OrderCreatedAt = item.OrderCreatedAt
                    }).OrderBy(item => item.OrderCreatedAt).ToList() // FIFO within group
                })
                .OrderBy(g => g.OldestOrderTime) // FIFO between groups
                .ToList();

            _logger.LogInformation("Retrieved {Count} grouped kitchen items for shop {ShopId}", groupedItems.Count, shopId);
            return groupedItems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting kitchen items for shop {ShopId}", shopId);
            return new List<KitchenItemGroupDto>();
        }
    }

    // 📝 STATUS MANAGEMENT
    public async Task<bool> UpdateItemStatusAsync(KitchenStatusUpdateDto update, Guid userId)
    {
        var orderItem = await _context.OrderItems
            .Include(oi => oi.Order)
            .FirstOrDefaultAsync(oi => oi.Id == update.OrderItemId);

        if (orderItem == null)
        {
            _logger.LogWarning("OrderItem {OrderItemId} not found", update.OrderItemId);
            return false;
        }

        var oldStatus = orderItem.KitchenStatus;
        orderItem.KitchenStatus = update.NewStatus;
        orderItem.UpdatedAt = DateTime.UtcNow;

        // Check if all items in the order are completed
        if (update.NewStatus == KitchenStatus.Completed)
        {
            var remainingItems = await _context.OrderItems
                .Where(oi => oi.OrderId == orderItem.OrderId && oi.Id != update.OrderItemId)
                .ToListAsync();

            if (remainingItems.All(oi => oi.KitchenStatus == KitchenStatus.Completed))
            {
                orderItem.Order.KitchenStatus = KitchenStatus.Completed;
                orderItem.Order.CompletedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated OrderItem {OrderItemId} status from {OldStatus} to {NewStatus} by user {UserId}", 
            update.OrderItemId, oldStatus, update.NewStatus, userId);

        return true;
    }

    // 🔍 ORDER TRACKING
    public async Task<KitchenStatus?> GetOrderKitchenStatusAsync(Guid orderId)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId);

        return order?.KitchenStatus;
    }

    public async Task<List<GroupedOrderItemDto>> GetOrderItemsAsync(Guid orderId)
    {
        var orderItems = await _context.OrderItems
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
            .CountAsync(oi => oi.Order.TenantId.Value == shopId && oi.KitchenStatus == KitchenStatus.Pending);
    }

    public async Task<TimeSpan> GetAveragePreparationTimeAsync(Guid shopId, DateTime from)
    {
        var completedItems = await _context.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.Order.TenantId.Value == shopId && 
                        oi.KitchenStatus == KitchenStatus.Completed &&
                        oi.Order.CompletedAt.HasValue &&
                        oi.Order.OrderDate >= from)
            .ToListAsync();

        if (!completedItems.Any())
            return TimeSpan.Zero;

        var averageTicks = completedItems
            .Average(oi => (oi.Order.CompletedAt!.Value - oi.Order.OrderDate).Ticks);

        return TimeSpan.FromTicks((long)averageTicks);
    }

    // 🎤 VOICE NOTE HANDLING (CORRECTED SIGNATURE)
    public async Task<VoiceNoteDto> ProcessVoiceNoteAsync(Guid orderId, VoiceNoteDto inputDto)
    {
        // 🛡️ DEFENSIVE: Apply size constraints
        var processedText = inputDto.Text;
        var processedAudioBlob = inputDto.AudioBlob;
        var transcriptionSuccessful = inputDto.TranscriptionSuccessful;

        // Text constraint: Max 500 characters
        if (!string.IsNullOrEmpty(processedText) && processedText.Length > 500)
        {
            processedText = processedText.Substring(0, 500);
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
        var order = await _context.Orders.FindAsync(orderId);
        if (order != null)
        {
            order.VoiceNoteText = processedText;
            order.VoiceNoteAudioBlob = processedAudioBlob;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        var result = new VoiceNoteDto
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
        var orderItem = await _context.OrderItems.FindAsync(orderItemId);
        if (orderItem == null)
        {
            _logger.LogWarning("OrderItem {OrderItemId} not found for voice note attachment", orderItemId);
            return false;
        }

        // 🛡️ DEFENSIVE: Apply size constraints
        orderItem.ItemNoteText = voiceNote.Text?.Length > 500 ? voiceNote.Text.Substring(0, 500) : voiceNote.Text;
        orderItem.ItemNoteAudioBlob = voiceNote.AudioBlob?.Length > 150000 ? null : voiceNote.AudioBlob;
        orderItem.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Attached voice note to OrderItem {OrderItemId}", orderItemId);
        return true;
    }

    public async Task<KitchenAnalyticsDto> GetKitchenAnalyticsAsync(Guid shopId, DateTime from)
    {
        var orders = await _context.Orders
            .Where(o => o.TenantId.Value == shopId && o.OrderDate >= from)
            .ToListAsync();

        var completedOrders = orders.Where(o => o.Status.Value == "Completed").ToList();
        var pendingOrders = orders.Where(o => o.Status.Value == "Pending" || o.Status.Value == "Preparing").ToList();

        return new KitchenAnalyticsDto
        {
            ShopId = shopId,
            PeriodStart = from,
            PeriodEnd = DateTime.UtcNow,
            TotalOrders = orders.Count(),
            CompletedOrders = completedOrders.Count(),
            PendingOrders = pendingOrders.Count(),
            AveragePreparationTime = completedOrders.Any() 
                ? (double)completedOrders.Average(o => (o.CompletedAt - o.OrderDate)?.TotalMinutes ?? 0)
                : 0,
            Performance = new List<KitchenPerformanceDto>()
        };
    }
}
