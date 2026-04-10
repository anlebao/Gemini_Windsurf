using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Models
{
    public class OrderInfo
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = "Đang chuẩn bị";
        public int EstimatedMinutes { get; set; } = 15;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        public IReadOnlyCollection<CartItem> Items { get; init; } = new List<CartItem>();
        
        // Computed properties
        public string DisplayId => Id.ToString()[..8];
        public string StatusDisplay => Status switch
        {
            "Đang chuẩn bị" => "⏳ Đang chuẩn bị",
            "Đã xác nhận" => "✅ Đã xác nhận", 
            "Đang pha chế" => "🔥 Đang pha chế",
            "Sẵn sàng" => "🎯 Sẵn sàng",
            "Đang giao" => "🚚 Đang giao",
            "Hoàn thành" => "🎉 Hoàn thành",
            _ => Status
        };
    }
}
