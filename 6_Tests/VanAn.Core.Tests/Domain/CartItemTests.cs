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
            item.TotalPrice.Should().Be(75000m);
        }

        [Fact(DisplayName = "ProductId is distinct from the cart line Id")]
        public void ProductId_IsDistinctFromCartLineId()
        {
            Guid productId = Guid.NewGuid();
            Shared.Domain.CartItem item = TestEntityBuilder.CreateCartItem(productId: productId);

            item.ProductId.Should().Be(productId);
            item.Id.Should().NotBe(productId);
        }

        [Fact(DisplayName = "With expression produces new instance with updated Quantity, Id unchanged")]
        public void WithExpression_UpdatesQuantityImmutably()
        {
            Shared.Domain.CartItem original = TestEntityBuilder.CreateCartItem(quantity: 1);
            Shared.Domain.CartItem updated = original with { Quantity = 5 };

            updated.Quantity.Should().Be(5);
            original.Quantity.Should().Be(1);
            updated.Id.Should().Be(original.Id);
            updated.ProductId.Should().Be(original.ProductId);
        }

        [Fact(DisplayName = "TotalPrice is zero when Quantity is zero")]
        public void TotalPrice_IsZero_WhenQuantityIsZero()
        {
            Shared.Domain.CartItem item = TestEntityBuilder.CreateCartItem(quantity: 0);
            item.TotalPrice.Should().Be(0m);
        }

        [Fact(DisplayName = "ProductName is not null or empty after construction")]
        public void ProductName_IsNotNullOrEmpty()
        {
            Shared.Domain.CartItem item = TestEntityBuilder.CreateCartItem(productName: "Cà phê đen");
            item.ProductName.Should().NotBeNullOrEmpty();
            item.ProductName.Should().Be("Cà phê đen");
        }

        [Fact(DisplayName = "Two CartItems with same ProductId but different Id are allowed by design")]
        public void SameProductId_DifferentCartLineId_AllowedByDesign()
        {
            Guid sharedProductId = Guid.NewGuid();
            Shared.Domain.CartItem item1 = TestEntityBuilder.CreateCartItem(productId: sharedProductId);
            Shared.Domain.CartItem item2 = TestEntityBuilder.CreateCartItem(productId: sharedProductId);

            item1.ProductId.Should().Be(item2.ProductId);
            item1.Id.Should().NotBe(item2.Id);
        }
    }
}
