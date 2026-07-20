using VanAn.KhachLink.Models;
using VanAn.KhachLink.Services;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Domain
{
    /// <summary>
    /// Phase 5: CartItem must carry TenantId so KhachLink can tell Gateway which tenant
    /// each cart item belongs to (multi-tenant cart → multi-order checkout).
    /// </summary>
    public class CartItemTenantIdTests
    {
        private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        [Fact]
        public void CartItem_TenantId_DefaultsToEmpty_WhenNotSet()
        {
            var item = new CartItem
            {
                Id = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                ProductName = "Test",
                Description = "",
                Quantity = 1,
                UnitPrice = 10000m,
                VatRate = 0.10m
            };

            // Default Guid.Empty — backward compat with code paths that don't yet set TenantId.
            Assert.Equal(Guid.Empty, item.TenantId);
        }

        [Fact]
        public void CartItem_WithTenantId_PreservesTenantId()
        {
            var productId = Guid.NewGuid();
            var item = new CartItem
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                ProductName = "Phở bò",
                Description = "",
                Quantity = 2,
                UnitPrice = 50000m,
                VatRate = 0.08m,
                TenantId = TenantA
            };

            Assert.Equal(TenantA, item.TenantId);
        }

        [Fact]
        public void CartItem_WithExpression_PreservesTenantId()
        {
            var item = new CartItem
            {
                Id = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                ProductName = "Cà phê",
                Description = "",
                Quantity = 1,
                UnitPrice = 30000m,
                VatRate = 0.08m,
                TenantId = TenantB
            };

            // `with` copy expression must preserve TenantId (init-only property).
            var updated = item with { Quantity = 3 };
            Assert.Equal(TenantB, updated.TenantId);
            Assert.Equal(3, updated.Quantity);
        }

        [Fact]
        public void CartState_AddItem_SetsTenantIdFromProduct()
        {
            var product = new ProductDto
            {
                ProductId = Guid.NewGuid(),
                Name = "Bún chả",
                Description = "Hà Nội",
                Price = 45000m,
                VatRate = 0.10m,
                TenantId = TenantA
            };

            var cart = new CartState();
            cart.AddItem(product, quantity: 1);

            Assert.Single(cart.Items);
            Assert.Equal(TenantA, cart.Items[0].TenantId);
        }

        [Fact]
        public void CartState_AddItem_FromTwoTenants_KeepsBothTenantIds()
        {
            var productA = new ProductDto
            {
                ProductId = Guid.NewGuid(),
                Name = "Phở",
                Price = 50000m,
                VatRate = 0.08m,
                TenantId = TenantA
            };
            var productB = new ProductDto
            {
                ProductId = Guid.NewGuid(),
                Name = "Cơm gà",
                Price = 35000m,
                VatRate = 0.10m,
                TenantId = TenantB
            };

            var cart = new CartState();
            cart.AddItem(productA, 1);
            cart.AddItem(productB, 1);

            Assert.Equal(2, cart.Items.Count);
            Assert.Contains(cart.Items, i => i.TenantId == TenantA);
            Assert.Contains(cart.Items, i => i.TenantId == TenantB);
        }
    }
}
