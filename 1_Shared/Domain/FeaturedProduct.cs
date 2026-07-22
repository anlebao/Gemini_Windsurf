using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Phase 6 (Admin UI): Value object for FeaturedProduct business key.
    /// Follows Single-Identity Pattern â€” ignored in EF config (Id = PK only).
    /// </summary>
    public record FeaturedProductId(Guid Value)
    {
        public static implicit operator Guid(FeaturedProductId id) => id.Value;
        public static implicit operator FeaturedProductId(Guid value) => new(value);
        public static FeaturedProductId FromGuid(Guid value) => new(value);
    }

    /// <summary>
    /// Phase 6 (Admin UI): Sysadmin-curated marketing product for Home.razor discovery.
    /// PG-only entity (NOT in ShopERP SQLite). Holds marketing display info (name, price, image)
    /// that may differ from the operational Product in ShopERP. ProductId + TenantId reference
    /// the product in ShopERP SQLite (validated at checkout via Phase 5 price validation).
    /// Follows Single-Identity Pattern: Id = PK, FeaturedProductId VO is ignored in EF config.
    /// </summary>
    public class FeaturedProduct : BaseEntity
    {
        public FeaturedProductId FeaturedProductId { get; protected set; } = new(Guid.NewGuid());

        /// <summary>Business reference to Product (in ShopERP SQLite, not PG FK).</summary>
        public Guid ProductId { get; protected set; }

        /// <summary>Marketing name (may differ from Product.Name).</summary>
        public string DisplayName { get; protected set; } = string.Empty;

        /// <summary>Marketing description.</summary>
        public string? DisplayDescription { get; protected set; }

        /// <summary>Marketing image URL.</summary>
        public string? ImageUrl { get; protected set; }

        /// <summary>Display price (may differ from actual â€” show "from" price).</summary>
        public decimal DisplayPrice { get; protected set; }

        /// <summary>VAT rate snapshot at time of featuring (e.g., 0.10 = 10%).
        /// Stored so KhachLink can add featured products to cart without scanning QR.
        /// May differ from Product.VatRate if product VAT changed after featuring.</summary>
        public decimal VatRate { get; protected set; } = 0.10m;

        public bool IsActive { get; protected set; } = true;

        /// <summary>Display ordering (lower = first).</summary>
        public int SortOrder { get; protected set; }

        /// <summary>When added to featured list.</summary>
        public DateTime FeaturedAt { get; protected set; }

        protected FeaturedProduct() { }

        public FeaturedProduct(TenantId tenantId, Guid productId, string displayName, decimal displayPrice,
            string? displayDescription = null, string? imageUrl = null, int sortOrder = 0, decimal vatRate = 0.10m)
            : base(tenantId)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("DisplayName cannot be empty.", nameof(displayName));
            if (displayPrice < 0)
                throw new ArgumentException("DisplayPrice cannot be negative.", nameof(displayPrice));
            if (vatRate < 0)
                throw new ArgumentException("VatRate cannot be negative.", nameof(vatRate));

            ProductId = productId;
            DisplayName = displayName;
            DisplayPrice = displayPrice;
            VatRate = vatRate;
            DisplayDescription = displayDescription;
            ImageUrl = imageUrl;
            SortOrder = sortOrder;
            IsActive = true;
            FeaturedAt = DateTime.UtcNow;
            // Single-Identity Pattern: PK == business key
            Id = FeaturedProductId.Value;
        }

        /// <summary>Factory with explicit Id (for tests + migrations).</summary>
        public static FeaturedProduct Create(Guid id, TenantId tenantId, Guid productId, string displayName,
            decimal displayPrice, string? displayDescription = null, string? imageUrl = null, int sortOrder = 0, decimal vatRate = 0.10m)
        {
            var fp = new FeaturedProduct(tenantId, productId, displayName, displayPrice, displayDescription, imageUrl, sortOrder, vatRate);
            fp.Id = id;
            fp.FeaturedProductId = new FeaturedProductId(id);
            return fp;
        }

        public void UpdateDisplayInfo(string displayName, decimal displayPrice, string? displayDescription,
            string? imageUrl, int sortOrder, decimal? vatRate = null)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("DisplayName cannot be empty.", nameof(displayName));
            if (displayPrice < 0)
                throw new ArgumentException("DisplayPrice cannot be negative.", nameof(displayPrice));

            DisplayName = displayName;
            DisplayPrice = displayPrice;
            if (vatRate.HasValue && vatRate.Value >= 0)
                VatRate = vatRate.Value;
            DisplayDescription = displayDescription;
            ImageUrl = imageUrl;
            SortOrder = sortOrder;
            UpdateAudit();
        }

        public void SetActive(bool isActive)
        {
            IsActive = isActive;
            UpdateAudit();
        }
    }
}
