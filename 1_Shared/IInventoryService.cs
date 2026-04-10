using VanAn.Shared.Domain;

namespace VanAn.Shared.Services;

public interface IInventoryService
{
    Task<bool> CanFulfillOrderAsync(Order order, IReadOnlyDictionary<IngredientId, Inventory> inventories, IReadOnlyDictionary<Guid, Recipe> recipes);
    Task<IReadOnlyDictionary<IngredientId, decimal>> CalculateIngredientDeductionAsync(Order order, IReadOnlyDictionary<Guid, Recipe> recipes);
    Task<IReadOnlyDictionary<IngredientId, Inventory>> UpdateInventoryAsync(IReadOnlyDictionary<IngredientId, Inventory> inventories, IReadOnlyDictionary<IngredientId, decimal> deductions);
}
