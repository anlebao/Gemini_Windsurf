using FluentAssertions;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Domain
{
    /// <summary>
    /// Product Pending-Review (2026-11-25, plan product-pending-review-master-plan D1):
    /// ProductStatus lifecycle — Ready (default) ↔ Pending (chờ owner duyệt).
    /// Pending ẩn khỏi public catalog; Ready mới dùng được trên storefront/cart.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Entity", "Product")]
    public class ProductStatusTests
    {
        private static readonly TenantId Tenant = new(Guid.NewGuid());

        [Fact(DisplayName = "Ctor default → Status = Ready")]
        public void Ctor_DefaultStatus_IsReady()
        {
            var product = new Product(Tenant, "Cà phê sữa đá", "Mô tả", 25000m, "Đồ uống");

            product.Status.Should().Be(ProductStatus.Ready);
        }

        [Fact(DisplayName = "Ctor với status=Pending → Status = Pending")]
        public void Ctor_WithPendingStatus_StatusPending()
        {
            var product = new Product(Tenant, "Spa dưỡng da", "Dịch vụ", 500000m, "Spa",
                status: ProductStatus.Pending);

            product.Status.Should().Be(ProductStatus.Pending);
        }

        [Fact(DisplayName = "MarkPending → Status Pending; MarkReady → Status Ready")]
        public void StatusTransitions_MarkPending_ThenMarkReady()
        {
            var product = new Product(Tenant, "Test", "", 1000m, "Cat");

            product.MarkPending();
            product.Status.Should().Be(ProductStatus.Pending);

            product.MarkReady();
            product.Status.Should().Be(ProductStatus.Ready);
        }

        [Fact(DisplayName = "MarkPending trên product Pending → idempotent, không throw")]
        public void MarkPending_Idempotent()
        {
            var product = new Product(Tenant, "Test", "", 1000m, "Cat", status: ProductStatus.Pending);

            var act = () => product.MarkPending();

            act.Should().NotThrow();
            product.Status.Should().Be(ProductStatus.Pending);
        }

        [Fact(DisplayName = "IsActive độc lập với Status — Pending + IsActive=true vẫn hợp lệ")]
        public void Status_IndependentOfIsActive()
        {
            var product = new Product(Tenant, "Test", "", 1000m, "Cat",
                isActive: true, status: ProductStatus.Pending);

            product.IsActive.Should().BeTrue();
            product.Status.Should().Be(ProductStatus.Pending);
        }

        [Fact(DisplayName = "Update() không thay đổi Status — lifecycle riêng")]
        public void Update_DoesNotChangeStatus()
        {
            var product = new Product(Tenant, "Old", "", 1000m, "Cat", status: ProductStatus.Pending);

            product.Update("New", "desc", 2000m, "Cat2", true, null, 0.08m);

            product.Status.Should().Be(ProductStatus.Pending);
        }
    }
}
