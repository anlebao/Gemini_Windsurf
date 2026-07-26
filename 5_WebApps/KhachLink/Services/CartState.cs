using VanAn.KhachLink.Models;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;

namespace VanAn.KhachLink.Services
{
    public class CartState
    {
        public List<CartItem> Items { get; set; } = [];
        /// <summary>Issue 4: Customer note for the order (entered on cart page, sent at checkout).</summary>
        public string OrderNote { get; set; } = string.Empty;
        public decimal SubTotal => Items.Sum(item => item.TotalPrice);
        public decimal NetSubTotal => Items.Sum(item => item.NetAmount);
        public decimal TotalVatAmount => Items.Sum(item => item.VatAmount);
        public decimal TotalAmount => Items.Sum(item => item.TotalPrice);

        public void AddItem(ProductDto product, int quantity = 1)
        {
            if (quantity <= 0)
            {
                return;
            }

            CartItem? existingItem = Items.FirstOrDefault(i => i.ProductId == product.ProductId);
            if (existingItem != null)
            {
                // CartItem is immutable, create new instance with updated quantity
                int index = Items.IndexOf(existingItem);
                Items[index] = existingItem with { Quantity = existingItem.Quantity + quantity };
            }
            else
            {
                Items.Add(new CartItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.ProductId,
                    ProductName = product.Name,
                    Description = product.Description ?? string.Empty,
                    Quantity = quantity,
                    UnitPrice = product.Price,
                    VatRate = product.VatRate,
                    TenantId = product.TenantId,
                    TenantName = product.TenantName ?? string.Empty
                });
            }
        }

        public void RemoveItem(Guid productId)
        {
            _ = Items.RemoveAll(i => i.ProductId == productId);
        }

        public void UpdateQuantity(Guid productId, int quantity)
        {
            if (quantity <= 0)
            {
                RemoveItem(productId);
                return;
            }

            CartItem? item = Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                // CartItem is immutable, create new instance with updated quantity
                int index = Items.IndexOf(item);
                Items[index] = item with { Quantity = quantity };
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
}
