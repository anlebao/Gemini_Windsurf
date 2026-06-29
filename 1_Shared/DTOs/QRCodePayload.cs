namespace VanAn.Shared.DTOs
{
    /// <summary>
    /// QR code data structure for product scanning
    /// Format: JSON with ProductId, ShopId, and Timestamp
    /// </summary>
    public class QRCodePayload
    {
        public Guid ProductId { get; set; }
        public Guid ShopId { get; set; }
        public long Timestamp { get; set; }

        public QRCodePayload(Guid productId, Guid shopId)
        {
            ProductId = productId;
            ShopId = shopId;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public string ToJson()
        {
            return System.Text.Json.JsonSerializer.Serialize(this);
        }

        public static QRCodePayload? FromJson(string json)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<QRCodePayload>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}