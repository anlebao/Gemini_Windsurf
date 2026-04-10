using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Services;

public class CartItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public int Quantity { get; set; }
    public decimal SubTotal => UnitPrice * Quantity;
    public decimal VatAmount => SubTotal * VatRate;
    public decimal TotalAmount => SubTotal + VatAmount;
}

public class CartState
{
    public List<CartItem> Items { get; set; } = new();
    public decimal SubTotal => Items.Sum(item => item.SubTotal);
    public decimal TotalVatAmount => Items.Sum(item => item.VatAmount);
    public decimal TotalAmount => Items.Sum(item => item.TotalAmount);
    
    public void AddItem(Product product, int quantity = 1)
    {
        if (quantity <= 0) return;
        
        var existingItem = Items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
        }
        else
        {
            Items.Add(new CartItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductDescription = product.Description,
                UnitPrice = product.Price,
                VatRate = product.VatRate,
                Quantity = quantity
            });
        }
    }
    
    public void RemoveItem(Guid productId)
    {
        Items.RemoveAll(i => i.ProductId == productId);
    }
    
    public void UpdateQuantity(Guid productId, int quantity)
    {
        if (quantity <= 0)
        {
            RemoveItem(productId);
            return;
        }
        
        var item = Items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            item.Quantity = quantity;
        }
    }
    
    public void Clear()
    {
        Items.Clear();
    }
    
    public int GetTotalItems()
    {
        return Items.Sum(i => i.Quantity);
    }
}
