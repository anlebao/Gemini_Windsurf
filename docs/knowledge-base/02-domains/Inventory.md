# Inventory Domain

> **Quản lý nguyên liệu, công thức và tồn kho**

## Overview

Inventory domain quản lý toàn bộ vòng đời nguyên liệu từ nhập kho đến sử dụng trong sản phẩm, bao gồm recipe management và tự động deduct khi bán hàng.

## Entities

### Ingredient

```csharp
public class Ingredient : BaseEntity
{
    public IngredientId IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty; // g, ml, cái
    public decimal CurrentStock { get; set; }
    public decimal MinStockThreshold { get; set; } // Alert when below
    public decimal PricePerUnit { get; set; }
    public bool IsActive { get; set; } = true;
}
```

### Recipe

```csharp
public class Recipe : BaseEntity
{
    public RecipeId RecipeId { get; set; }
    public Guid ProductId { get; set; }  // Link to Product
    public Guid IngredientId { get; set; }  // Link to Ingredient
    public decimal QuantityNeeded { get; set; }  // Per 1 product unit
    
    // Navigation
    public Product Product { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
```

### Inventory (Stock Record)

```csharp
public class Inventory : BaseEntity
{
    public InventoryId InventoryId { get; protected set; }
    public Guid IngredientId { get; protected set; }
    public decimal Quantity { get; protected set; }
    public DateTime LastUpdated { get; protected set; }
    
    public void UpdateQuantity(decimal newQuantity)
    {
        Quantity = newQuantity;
        LastUpdated = DateTime.UtcNow;
        UpdateAudit();
    }
}
```

## Concepts

### Recipe Structure

```
Product (Cà phê sữa đá)
├── Ingredient: Coffee (20g)
├── Ingredient: Condensed milk (15ml)
├── Ingredient: Ice (50g)
└── Ingredient: Water (100ml)
```

### Stock Calculation

```csharp
// When order confirmed with 2 Cà phê sữa đá
coffeeDeduct = 2 * 20g = 40g
milkDeduct = 2 * 15ml = 30ml
iceDeduct = 2 * 50g = 100g
waterDeduct = 2 * 100ml = 200ml
```

### Stock Alerts

| Level | Condition | Action |
|-------|-----------|--------|
| Normal | Stock > MinThreshold | None |
| Warning | Stock <= MinThreshold | UI warning, notification |
| Critical | Stock <= 0 | Block orders using this ingredient |

## Business Rules

### BR-001: Recipe Completeness
- Mỗi Product có thể có 0 hoặc nhiều Recipe items
- Product không có recipe = service-only (không tốn nguyên liệu)
- Recipe item quantity phải > 0

### BR-002: Stock Deduction
```csharp
// Khi Order Confirmed
foreach (var item in order.Items)
{
    var recipes = _recipeRepo.GetByProductId(item.ProductId);
    foreach (var recipe in recipes)
    {
        var deductAmount = recipe.QuantityNeeded * item.Quantity;
        _inventoryService.Deduct(recipe.IngredientId, deductAmount);
    }
}
```

### BR-003: Stock Restoration
- Khi Confirmed → Cancelled: Restore toàn bộ stock
- Khi Confirmed → Cancelled (một phần): Restore tương ứng

### BR-004: Negative Stock Prevention
- **Option A**: Block order (hard constraint)
- **Option B**: Allow negative (soft constraint + alert)
- **ShopERP chọn A**: Không cho phép negative stock

### BR-005: Unit Conversion
- Tất cả calculations trong cùng đơn vị (base unit)
- Conversion nếu cần: Purchase unit → Base unit
- Ví dụ: Mua "thùng" 24 lon, base unit = "lon"

## Inventory Transactions

```csharp
public enum InventoryTransactionType
{
    Purchase = 1,      // Nhập kho
    Sale = 2,          // Bán hàng (auto deduct)
    Adjustment = 3,    // Điều chỉnh (kiểm kê)
    Waste = 4,         // Hao hụt, hủy
    Return = 5         // Trả hàng
}

public class InventoryTransaction : BaseEntity
{
    public Guid IngredientId { get; set; }
    public InventoryTransactionType Type { get; set; }
    public decimal QuantityChange { get; set; }  // Positive or negative
    public decimal StockAfter { get; set; }
    public string? ReferenceId { get; set; }  // OrderId, PurchaseId
    public string? Notes { get; set; }
}
```

## Integration Points

| Domain | Integration | Trigger |
|--------|-------------|---------|
| Order | Auto deduct stock | Order → Confirmed |
| Order | Restore stock | Confirmed → Cancelled |
| Product | Recipe management | Product created/updated |
| Accounting | COGS calculation | Inventory transaction |
| Reporting | Stock reports | Daily/weekly/monthly |

## Use Cases

### UC-001: Check Stock Availability
```csharp
public bool CanFulfillOrder(Guid productId, int quantity)
{
    var recipes = _recipeRepo.GetByProductId(productId);
    foreach (var recipe in recipes)
    {
        var needed = recipe.QuantityNeeded * quantity;
        var available = _inventoryRepo.GetStock(recipe.IngredientId);
        if (available < needed) return false;
    }
    return true;
}
```

### UC-002: Record Purchase
```csharp
public void RecordPurchase(Guid ingredientId, decimal quantity, decimal price)
{
    // Update inventory
    var inventory = _inventoryRepo.GetByIngredient(ingredientId);
    inventory.UpdateQuantity(inventory.Quantity + quantity);
    
    // Create transaction record
    var transaction = new InventoryTransaction
    {
        IngredientId = ingredientId,
        Type = InventoryTransactionType.Purchase,
        QuantityChange = quantity,
        StockAfter = inventory.Quantity
    };
    _transactionRepo.Add(transaction);
}
```

### UC-003: Stock Adjustment (Kiểm Kê)
```csharp
public void AdjustStock(Guid ingredientId, decimal actualQuantity, string reason)
{
    var inventory = _inventoryRepo.GetByIngredient(ingredientId);
    var difference = actualQuantity - inventory.Quantity;
    
    inventory.UpdateQuantity(actualQuantity);
    
    _transactionRepo.Add(new InventoryTransaction
    {
        IngredientId = ingredientId,
        Type = InventoryTransactionType.Adjustment,
        QuantityChange = difference,
        StockAfter = actualQuantity,
        Notes = $"Kiểm kê: {reason}"
    });
}
```

## DTOs

### StockResponse
```csharp
public class StockResponse
{
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = "";
    public decimal CurrentStock { get; set; }
    public decimal MinThreshold { get; set; }
    public string Status { get; set; } = "normal"; // normal, warning, critical
}
```

### RecipeRequest
```csharp
public class RecipeRequest
{
    public Guid ProductId { get; set; }
    public List<RecipeItemRequest> Items { get; set; } = new();
}

public class RecipeItemRequest
{
    public Guid IngredientId { get; set; }
    public decimal QuantityNeeded { get; set; }
}
```

## Events

```csharp
public record StockLow(IngredientId IngredientId, decimal CurrentStock, decimal Threshold);
public record StockOut(IngredientId IngredientId);
public record IngredientDeducted(IngredientId IngredientId, decimal Amount, OrderId OrderId);
public record IngredientRestored(IngredientId IngredientId, decimal Amount, OrderId OrderId);
```

## Validation Rules

- **Ingredient Name**: Required, max 200 chars
- **Unit**: Required, valid unit (g, kg, ml, l, cái, hộp, ...)
- **MinStockThreshold**: >= 0
- **QuantityNeeded**: > 0
- **Product-Ingredient pair**: Unique (1 ingredient per product chỉ 1 recipe item)

## References

- `1_Shared/Domain.cs` - Ingredient, Recipe, Inventory entities
- `docs/design/` - Database schema

---

*Last Updated: June 1, 2026*
