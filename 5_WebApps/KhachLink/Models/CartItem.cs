namespace VanAn.KhachLink.Models
{
    public class CartItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Price { get; set; }
    }
}
