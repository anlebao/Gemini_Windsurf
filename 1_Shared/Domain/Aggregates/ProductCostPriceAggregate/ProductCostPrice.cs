using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.ProductCostPriceAggregate
{
    /// <summary>
    /// ProductCostPrice — Vạn An's negotiated cost price per product per tenant — Sprint 7 (Q1).
    /// Lives in Gateway PG (not ShopERP SQLite — Product lives there, this is a reference table).
    /// Set by Van An admin via POST /api/admin/product-cost-price. Tenant negotiates offline.
    /// Unique index on (TenantId, ProductId).
    /// </summary>
    public class ProductCostPrice : BaseEntity, IMustHaveTenant
    {
        public Guid ProductId { get; protected set; }
        public decimal CostPrice { get; protected set; }
        public new DateTime? UpdatedAt { get; protected set; }
        public new Guid? UpdatedBy { get; protected set; }

        protected ProductCostPrice() { }

        public ProductCostPrice(TenantId tenantId, Guid productId, decimal costPrice, Guid? updatedBy = null)
            : base(tenantId)
        {
            if (productId == Guid.Empty)
                throw new ArgumentException("ProductId cannot be empty", nameof(productId));
            if (costPrice < 0)
                throw new ArgumentOutOfRangeException(nameof(costPrice), "CostPrice cannot be negative");

            ProductId = productId;
            CostPrice = costPrice;
            UpdatedAt = DateTime.UtcNow;
            UpdatedBy = updatedBy;
        }

        public void Update(decimal costPrice, Guid? updatedBy = null)
        {
            if (costPrice < 0)
                throw new ArgumentOutOfRangeException(nameof(costPrice), "CostPrice cannot be negative");

            CostPrice = costPrice;
            UpdatedAt = DateTime.UtcNow;
            UpdatedBy = updatedBy;
            UpdateAudit();
        }
    }
}
