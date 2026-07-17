using VanAn.Core.Tests.TestInfrastructure;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Domain
{
    [Trait("Category", "Unit")]
    [Trait("Entity", "CartItem")]
    public class CartItemTests
    {
        [Fact(DisplayName = "TotalPrice equals Quantity times UnitPrice")]
        public void TotalPrice_IsQuantityTimesUnitPrice()
        {
            Shared.Domain.CartItem item = TestEntityBuilder.CreateCartItem(quantity: 3, unitPrice: 25000m);
            _ = item.TotalPrice.Should().Be(75000m);
        }

        [Fact(DisplayName = "ProductId is distinct from the cart line Id")]
        public void ProductId_IsDistinctFromCartLineId()
        {
            Guid productId = Guid.NewGuid();
            Shared.Domain.CartItem item = TestEntityBuilder.CreateCartItem(productId: productId);

            _ = item.ProductId.Should().Be(productId);
            _ = item.Id.Should().NotBe(productId);
        }

        [Fact(DisplayName = "With expression produces new instance with updated Quantity, Id unchanged")]
        public void WithExpression_UpdatesQuantityImmutably()
        {
            Shared.Domain.CartItem original = TestEntityBuilder.CreateCartItem(quantity: 1);
            Shared.Domain.CartItem updated = original with { Quantity = 5 };

            _ = updated.Quantity.Should().Be(5);
            _ = original.Quantity.Should().Be(1);
            _ = updated.Id.Should().Be(original.Id);
            _ = updated.ProductId.Should().Be(original.ProductId);
        }

        [Fact(DisplayName = "TotalPrice is zero when Quantity is zero")]
        public void TotalPrice_IsZero_WhenQuantityIsZero()
        {
            Shared.Domain.CartItem item = TestEntityBuilder.CreateCartItem(quantity: 0);
            _ = item.TotalPrice.Should().Be(0m);
        }

        [Fact(DisplayName = "ProductName is not null or empty after construction")]
        public void ProductName_IsNotNullOrEmpty()
        {
            Shared.Domain.CartItem item = TestEntityBuilder.CreateCartItem(productName: "Cà phê đen");
            _ = item.ProductName.Should().NotBeNullOrEmpty();
            _ = item.ProductName.Should().Be("Cà phê đen");
        }

        [Fact(DisplayName = "Two CartItems with same ProductId but different Id are allowed by design")]
        public void SameProductId_DifferentCartLineId_AllowedByDesign()
        {
            Guid sharedProductId = Guid.NewGuid();
            Shared.Domain.CartItem item1 = TestEntityBuilder.CreateCartItem(productId: sharedProductId);
            Shared.Domain.CartItem item2 = TestEntityBuilder.CreateCartItem(productId: sharedProductId);

            _ = item1.ProductId.Should().Be(item2.ProductId);
            _ = item1.Id.Should().NotBe(item2.Id);
        }

        // ============================================================================
        // RC-7: VAT computation tests — VAT-inclusive price extraction.
        // UnitPrice is gross (VAT-inclusive). VatAmount = TotalPrice - (TotalPrice / (1 + VatRate)).
        // ============================================================================

        [Fact(DisplayName = "VatAmount extracts VAT from gross price at 10% rate")]
        public void VatAmount_ExtractsVatFromGrossPrice_AtTenPercent()
        {
            // Gross price 11000, VAT 10% → net = 10000, VAT = 1000
            Shared.Domain.CartItem item = new()
            {
                Id = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                ProductName = "Cà phê",
                Description = "",
                Quantity = 1,
                UnitPrice = 11000m,
                VatRate = 0.10m
            };
            _ = item.TotalPrice.Should().Be(11000m);
            _ = item.VatAmount.Should().Be(1000m);
            _ = item.NetAmount.Should().Be(10000m);
        }

        [Fact(DisplayName = "VatAmount scales with quantity")]
        public void VatAmount_ScalesWithQuantity()
        {
            // 2 × 11000 = 22000 gross, VAT 10% → net = 20000, VAT = 2000
            Shared.Domain.CartItem item = new()
            {
                Id = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                ProductName = "Trà",
                Description = "",
                Quantity = 2,
                UnitPrice = 11000m,
                VatRate = 0.10m
            };
            _ = item.TotalPrice.Should().Be(22000m);
            _ = item.VatAmount.Should().Be(2000m);
            _ = item.NetAmount.Should().Be(20000m);
        }

        [Fact(DisplayName = "VatAmount is zero when VatRate is zero")]
        public void VatAmount_IsZero_WhenVatRateIsZero()
        {
            Shared.Domain.CartItem item = new()
            {
                Id = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                ProductName = "Nước lọc",
                Description = "",
                Quantity = 3,
                UnitPrice = 10000m,
                VatRate = 0.00m
            };
            _ = item.VatAmount.Should().Be(0m);
            _ = item.NetAmount.Should().Be(30000m);
            _ = item.TotalPrice.Should().Be(30000m);
        }

        [Fact(DisplayName = "VatAmount uses 5% rate correctly")]
        public void VatAmount_UsesFivePercentRate()
        {
            // Gross 105000, VAT 5% → net = 100000, VAT = 5000
            Shared.Domain.CartItem item = new()
            {
                Id = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                ProductName = "Trà sữa",
                Description = "",
                Quantity = 1,
                UnitPrice = 105000m,
                VatRate = 0.05m
            };
            _ = item.VatAmount.Should().Be(5000m);
            _ = item.NetAmount.Should().Be(100000m);
        }

        [Fact(DisplayName = "Default VatRate is 0.10m when not explicitly set")]
        public void DefaultVatRate_IsTenPercent()
        {
            Shared.Domain.CartItem item = TestEntityBuilder.CreateCartItem(unitPrice: 11000m);
            _ = item.VatRate.Should().Be(0.10m);
            _ = item.VatAmount.Should().Be(1000m);
        }
    }
}
