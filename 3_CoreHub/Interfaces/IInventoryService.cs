using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Interfaces;

/// <summary>
/// Inventory Service interface for stock management
/// Phase 2.5: Backend Consolidation
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Check if order can be fulfilled with current inventory
    /// </summary>
    Task<bool> CanFulfillOrderAsync(Order order, Dictionary<IngredientId, Inventory> currentInventory, Dictionary<Guid, Recipe> recipes);
    
    /// <summary>
    /// Deduct inventory for order
    /// </summary>
    Task DeductInventoryAsync(Order order);
    
    /// <summary>
    /// Get current inventory levels
    /// </summary>
    Task<Dictionary<IngredientId, Inventory>> GetCurrentInventoryAsync(TenantId tenantId);
    
    /// <summary>
    /// Update inventory levels
    /// </summary>
    Task UpdateInventoryAsync(IngredientId ingredientId, int quantity);
}
