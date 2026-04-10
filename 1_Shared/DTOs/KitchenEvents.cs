using VanAn.Shared.Domain;

namespace VanAn.Shared.DTOs;

/// <summary>
/// SignalR event for order confirmation
/// </summary>
public record OrderConfirmedEvent
{
    public Guid ShopId { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public DateTime ConfirmedAt { get; init; }
    public VoiceNoteDto? VoiceNote { get; init; }
}

/// <summary>
/// SignalR event for kitchen item status changes
/// </summary>
public record KitchenItemStatusChangedEvent
{
    public Guid OrderItemId { get; init; }
    public Guid OrderId { get; init; }
    public Guid ProductId { get; init; }
    public KitchenStatus OldStatus { get; init; }
    public KitchenStatus NewStatus { get; init; }
    public DateTime ChangedAt { get; init; }
    public string? ChangedBy { get; init; }
}
