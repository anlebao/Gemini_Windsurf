using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Services
{
    public class CartState
    {
        public List<CartItem> Items { get; set; } = [];
        public decimal SubTotal => Items.Sum(item => item.TotalPrice);
        public decimal TotalVatAmount => 0; // VAT not currently supported in Domain CartItem
        public decimal TotalAmount => Items.Sum(item => item.TotalPrice);

        public void AddItem(Product product, int quantity = 1)
        {
            if (quantity <= 0)
            {
                return;
            }

            CartItem? existingItem = Items.FirstOrDefault(i => i.ProductId == product.Id);
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
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Description = product.Description ?? string.Empty,
                    Quantity = quantity,
                    UnitPrice = product.Price
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
