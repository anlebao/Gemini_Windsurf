using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Models
{
    public class OrderInfo
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = "Äang chuáº©n bá»‹";
        public int EstimatedMinutes { get; set; } = 15;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        public IReadOnlyCollection<CartItem> Items { get; init; } = new List<CartItem>();

        // Computed properties
        public string DisplayId => Id.ToString()[..8];
        public string StatusDisplay => Status switch
        {
            "Äang chuáº©n bá»‹" => "â³ Äang chuáº©n bá»‹",
            "ÄÃ£ xÃ¡c nháº­n" => "âœ… ÄÃ£ xÃ¡c nháº­n",
            "Äang pha cháº¿" => "ðŸ”¥ Äang pha cháº¿",
            "Sáºµn sÃ ng" => "ðŸŽ¯ Sáºµn sÃ ng",
            "Äang giao" => "ðŸšš Äang giao",
            "HoÃ n thÃ nh" => "ðŸŽ‰ HoÃ n thÃ nh",
            _ => Status
        };
    }
}
