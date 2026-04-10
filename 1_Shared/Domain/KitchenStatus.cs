namespace VanAn.Shared.Domain;

/// <summary>
/// Kitchen workflow status for orders and items
/// </summary>
public enum KitchenStatus
{
    Pending = 0,      // Order received, awaiting kitchen
    Preparing = 1,   // Masterchef actively preparing
    Completed = 2,    // Item finished, ready for serving
    Cancelled = 3     // Order cancelled (kitchen-side)
}
