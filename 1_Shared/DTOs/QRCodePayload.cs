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
        /// W3-T7: Optional table number â€” only included when QR_TableNumber_Enabled = ON.
        /// Null when toggle OFF (backward compat â€” old QR codes still scan correctly).
        /// </summary>
        public string? TableNumber { get; set; }

        /// <summary>Phase 5: Product unit price snapshot at QR print time. 0 for legacy QR codes
        /// (printed before Phase 5) â€” Scan.razor falls back to API call when 0.</summary>
        public decimal UnitPrice { get; set; }

        /// <summary>Phase 5: VAT rate snapshot at QR print time. 0 for legacy QR codes.</summary>
        public decimal VatRate { get; set; }

        /// <summary>Phase 5: Product name snapshot at QR print time. Null for legacy QR codes.
        /// Lets Scan.razor display product name in cart without an API call.</summary>
        public string? ProductName { get; set; }

        /// <summary>Phase 5: Tenant ID that owns this product. Guid.Empty for legacy QR codes.
        /// Required for multi-tenant cart grouping â€” without it, checkout can't route items to the correct tenant.</summary>
        public Guid TenantId { get; set; }

        /// <summary>Product image URL snapshot at QR print time. Null for legacy QR codes (printed before this field).
        /// Lets Scan.razor display the product image in fast-path mode without an API call.</summary>
        public string? ImageUrl { get; set; }

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

        /// <summary>
        /// Phase 5: Constructor overload with price/VAT/name snapshot for fast offline scan.
        /// Use this overload when generating new QR codes so Scan.razor can skip the API call.
        /// </summary>
        public QRCodePayload(Guid productId, Guid shopId, string? tableNumber,
            decimal unitPrice, decimal vatRate, string? productName) : this(productId, shopId, tableNumber)
        {
            UnitPrice = unitPrice;
            VatRate = vatRate;
            ProductName = productName;
        }

        /// <summary>
        /// Phase 5: Full constructor with TenantId â€” required for multi-tenant cart grouping.
        /// Use this overload when generating new QR codes so Scan.razor can add to cart without ANY API call.
        /// </summary>
        public QRCodePayload(Guid productId, Guid shopId, string? tableNumber,
            decimal unitPrice, decimal vatRate, string? productName, Guid tenantId) : this(productId, shopId, tableNumber, unitPrice, vatRate, productName)
        {
            TenantId = tenantId;
        }

        /// <summary>
        /// Phase 5+: Full constructor with TenantId + ImageUrl — lets Scan.razor display product image
        /// in fast-path mode (no API call). Use this overload when generating new QR codes for products
        /// that have an image URL.
        /// </summary>
        public QRCodePayload(Guid productId, Guid shopId, string? tableNumber,
            decimal unitPrice, decimal vatRate, string? productName, Guid tenantId, string? imageUrl) : this(productId, shopId, tableNumber, unitPrice, vatRate, productName, tenantId)
        {
            ImageUrl = imageUrl;
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
        /// Parse QR content â€” supports both raw JSON (legacy) and URL format (Zalo-compatible).
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
                    // Parse query string manually (avoid System.Web.HttpUtility â€” not reliable on Linux)
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