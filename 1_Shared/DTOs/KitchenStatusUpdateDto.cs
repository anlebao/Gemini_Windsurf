using VanAn.Shared.Domain;

namespace VanAn.Shared.DTOs;

/// <summary>
/// Kitchen status update request
/// </summary>
public record KitchenStatusUpdateDto
{
    public Guid ShopId { get; init; }
    public Guid OrderItemId { get; init; }
    public KitchenStatus NewStatus { get; init; }
    public string? UpdatedBy { get; init; } // User who made the change
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}
