using VanAn.Shared.Domain;

namespace VanAn.Shared.DTOs;

/// <summary>
/// Kitchen item group for FIFO preparation workflow
/// </summary>
public record KitchenItemGroupDto
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int TotalQuantity { get; init; }
    public KitchenStatus GroupStatus { get; init; }
    public DateTime OldestOrderTime { get; init; }
    public List<GroupedOrderItemDto> Items { get; init; } = new();
}

/// <summary>
/// Individual order item within a kitchen group
/// </summary>
public record GroupedOrderItemDto
{
    public Guid OrderItemId { get; init; }
    public Guid OrderId { get; init; }
    public int Quantity { get; init; }
    public KitchenStatus Status { get; init; }
    public string? VoiceNoteText { get; init; }
    public string? VoiceNoteAudioBlob { get; init; }
    public DateTime OrderCreatedAt { get; init; }
}
