using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;

namespace VanAn.Shared.Services;

/// <summary>
/// Kitchen service for managing order preparation workflow with FIFO grouping
/// </summary>
public interface IKitchenService
{
    // 🎯 CORE FIFO GROUPING LOGIC
    Task<List<KitchenItemGroupDto>> GetGroupedKitchenItemsAsync(Guid shopId);
    
    // 📝 STATUS MANAGEMENT
    Task<bool> UpdateItemStatusAsync(KitchenStatusUpdateDto update, Guid userId);
    
    // 🔍 ORDER TRACKING
    Task<KitchenStatus?> GetOrderKitchenStatusAsync(Guid orderId);
    Task<List<GroupedOrderItemDto>> GetOrderItemsAsync(Guid orderId);
    
    // 📊 KITCHEN ANALYTICS
    Task<int> GetPendingItemsCountAsync(Guid shopId);
    Task<TimeSpan> GetAveragePreparationTimeAsync(Guid shopId, DateTime from);
    Task<KitchenAnalyticsDto> GetKitchenAnalyticsAsync(Guid shopId, DateTime from);
    
    // 🎤 VOICE NOTE HANDLING (CORRECTED SIGNATURE)
    Task<VoiceNoteDto> ProcessVoiceNoteAsync(Guid orderId, VoiceNoteDto inputDto);
    Task<bool> AttachVoiceNoteToItemAsync(Guid orderItemId, VoiceNoteDto voiceNote);
}
