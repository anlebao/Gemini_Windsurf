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
        /// <summary>
        /// W3-T7: Optional table number — only included when QR_TableNumber_Enabled = ON.
        /// Null when toggle OFF (backward compat — old QR codes still scan correctly).
        /// </summary>
        public string? TableNumber { get; set; }

        /// <summary>
        /// Parameterless constructor for JSON deserialization (JsonSerializer requires it).
        /// </summary>
        public QRCodePayload() { }

        public QRCodePayload(Guid productId, Guid shopId)
        {
            ProductId = productId;
            ShopId = shopId;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// W3-T7: Constructor overload with optional table number.
        /// </summary>
        public QRCodePayload(Guid productId, Guid shopId, string? tableNumber) : this(productId, shopId)
        {
            TableNumber = tableNumber;
        }

        public string ToJson()
        {
            return System.Text.Json.JsonSerializer.Serialize(this);
        }

        /// <summary>
        /// Issue 9 fix: Generate QR content as URL so external scanners (Zalo) can open it.
        /// Format: https://diemthuong.khachvip.online/scan?data={base64(json)}
        /// The Scan page detects URL format and extracts the embedded JSON.
        /// </summary>
        public string ToQrContent(string baseUrl = "https://diemthuong.khachvip.online")
        {
            string json = ToJson();
            string base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
            return $"{baseUrl.TrimEnd('/')}/scan?data={base64}";
        }

        /// <summary>
        /// Parse QR content — supports both raw JSON (legacy) and URL format (Zalo-compatible).
        /// URL format: https://diemthuong.khachvip.online/scan?data={base64(json)}
        /// </summary>
        public static QRCodePayload? FromJson(string qrContent)
        {
            try
            {
                // Issue 9: If QR contains a URL with ?data= param, extract base64-encoded JSON
                if (qrContent.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(qrContent);
                    // Parse query string manually (avoid System.Web.HttpUtility — not reliable on Linux)
                    string query = uri.Query.TrimStart('?');
                    string? dataParam = null;
                    foreach (string pair in query.Split('&'))
                    {
                        int eq = pair.IndexOf('=');
                        if (eq > 0 && pair[..eq] == "data")
                        {
                            dataParam = Uri.UnescapeDataString(pair[(eq + 1)..]);
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(dataParam))
                    {
                        string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(dataParam));
                        return System.Text.Json.JsonSerializer.Deserialize<QRCodePayload>(json);
                    }
                    return null;
                }

                // Legacy: raw JSON
                return System.Text.Json.JsonSerializer.Deserialize<QRCodePayload>(qrContent);
            }
            catch
            {
                return null;
            }
        }
    }
}