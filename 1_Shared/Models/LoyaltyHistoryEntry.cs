namespace VanAn.Shared.Models
{
    public class LoyaltyHistoryEntry
    {
        public string Type { get; set; } = string.Empty; // EARN or SPEND
        public int Points { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
