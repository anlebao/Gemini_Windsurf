using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services
{
    public class InventoryService(VanAnDbContext context, ILogger<InventoryService> logger) : IInventoryService
    {
        private readonly VanAnDbContext _context = context;
        private readonly ILogger<InventoryService> _logger = logger;

        public async Task<bool> CanFulfillOrderAsync(Order order, IReadOnlyDictionary<IngredientId, Inventory> inventories, IReadOnlyDictionary<Guid, Recipe> recipes)
        {
            try
            {
                IReadOnlyDictionary<IngredientId, decimal> deductions = await CalculateIngredientDeductionAsync(order, recipes);

                foreach (KeyValuePair<IngredientId, decimal> deduction in deductions)
                {
                    if (inventories.TryGetValue(deduction.Key, out Inventory? inventory))
                    {
                        if (inventory.Quantity < deduction.Value)
                        {
                            _logger.LogWarning("Insufficient inventory for ingredient {IngredientId}: required {Required}, available {Available}",
                                deduction.Key, deduction.Value, inventory.Quantity);
                            return false;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Inventory not found for ingredient {IngredientId}", deduction.Key);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking inventory fulfillment for order {OrderId}", order.Id);
                return false;
            }
        }

        public async Task<IReadOnlyDictionary<IngredientId, decimal>> CalculateIngredientDeductionAsync(Order order, IReadOnlyDictionary<Guid, Recipe> recipes)
        {
            await Task.CompletedTask;
            try
            {
                Dictionary<IngredientId, decimal> deductions = [];

                // For multi-item orders
                foreach (OrderItem item in order.Items)
                {
                    if (recipes.TryGetValue(item.ProductId, out Recipe? recipe))
                    {
                        decimal totalDeduction = recipe.QuantityNeeded * item.Quantity;

                        IngredientId ingredientId = new(recipe.IngredientId);
                        if (deductions.ContainsKey(ingredientId))
                        {
                            deductions[ingredientId] += totalDeduction;
                        }
                        else
                        {
                            deductions[ingredientId] = totalDeduction;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Recipe not found for product {ProductId}", item.ProductId);
                    }
                }

                return deductions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating ingredient deduction for order {OrderId}", order.Id);
                throw;
            }
        }

        public async Task<IReadOnlyDictionary<IngredientId, Inventory>> UpdateInventoryAsync(IReadOnlyDictionary<IngredientId, Inventory> inventories, IReadOnlyDictionary<IngredientId, decimal> deductions)
        {
            try
            {
                Dictionary<IngredientId, Inventory> updatedInventories = [];

                foreach (KeyValuePair<IngredientId, decimal> deduction in deductions)
                {
                    if (inventories.TryGetValue(deduction.Key, out Inventory? inventory))
                    {
                        decimal newQuantity = inventory.Quantity - deduction.Value;

                        if (newQuantity < 0)
                        {
                            _logger.LogWarning("Inventory would go negative for ingredient {IngredientId}: current {Current}, deduction {Deduction}",
                                deduction.Key, inventory.Quantity, deduction.Value);
                            continue;
                        }

                        inventory.UpdateQuantity(newQuantity);

                        updatedInventories[deduction.Key] = inventory;
                    }
                }

                // Update in database
                foreach (Inventory inventory in updatedInventories.Values)
                {
                    _context.Inventories.Update(inventory);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated {Count} inventory records", updatedInventories.Count);
                return updatedInventories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating inventory");
                throw;
            }
        }
    }
}
