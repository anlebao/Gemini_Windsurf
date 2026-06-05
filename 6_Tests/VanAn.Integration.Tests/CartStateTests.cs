using VanAn.Shared.Domain;
using VanAn.KhachLink.Services;
using VanAn.Integration.Tests.Infrastructure;
using Xunit;
using FluentAssertions;

namespace VanAn.Integration.Tests;

[Trait("Category", "Unit")]
[Trait("Service", "CartState")]
public class CartStateTests
{
    private readonly CartState _cartState;
    private readonly TenantId _tenantId;

    public CartStateTests()
    {
        _cartState = new CartState();
        _tenantId = TestEntityBuilder.CreateTenantId();
    }

    private Product MakeProduct(string name = "Test Product", decimal price = 25000m)
        => TestEntityBuilder.CreateProduct(_tenantId, name, price);

    [Fact(DisplayName = "AddItem — new product is added to cart with correct ProductId")]
    public void AddItem_NewProduct_AddsToCart()
    {
        var product = MakeProduct();

        _cartState.AddItem(product);

        _cartState.Items.Should().HaveCount(1);
        _cartState.Items[0].ProductId.Should().Be(product.Id);
    }

    [Fact(DisplayName = "AddItem — same product increments Quantity, does NOT create duplicate line")]
    public void AddItem_SameProduct_IncrementsQuantity_NotDuplicates()
    {
        var product = MakeProduct();

        _cartState.AddItem(product);
        _cartState.AddItem(product);

        _cartState.Items.Should().HaveCount(1);
        _cartState.Items[0].Quantity.Should().Be(2);
    }

    [Fact(DisplayName = "AddItem — different products both appear as separate lines")]
    public void AddItem_DifferentProducts_AddsBoth()
    {
        _cartState.AddItem(MakeProduct("Product A", 10000m));
        _cartState.AddItem(MakeProduct("Product B", 20000m));

        _cartState.Items.Should().HaveCount(2);
    }

    [Fact(DisplayName = "RemoveItem — removes the correct line by ProductId")]
    public void RemoveItem_ByProductId_RemovesCorrectLine()
    {
        var product = MakeProduct();
        _cartState.AddItem(product);

        _cartState.RemoveItem(product.Id);

        _cartState.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "UpdateQuantity — changes quantity of the correct line by ProductId")]
    public void UpdateQuantity_ByProductId_ChangesQuantity()
    {
        var product = MakeProduct();
        _cartState.AddItem(product);

        _cartState.UpdateQuantity(product.Id, 5);

        _cartState.Items[0].Quantity.Should().Be(5);
    }

    [Fact(DisplayName = "UpdateQuantity — zero quantity removes the item entirely")]
    public void UpdateQuantity_ZeroQuantity_RemovesItem()
    {
        var product = MakeProduct();
        _cartState.AddItem(product);

        _cartState.UpdateQuantity(product.Id, 0);

        _cartState.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "Clear — removes all items from the cart")]
    public void Clear_RemovesAllItems()
    {
        _cartState.AddItem(MakeProduct("A"));
        _cartState.AddItem(MakeProduct("B"));

        _cartState.Clear();

        _cartState.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "TotalAmount — sums TotalPrice (Quantity * UnitPrice) of all lines")]
    public void TotalAmount_SumsAllItemTotalPrices()
    {
        _cartState.AddItem(MakeProduct("A", 10000m));
        _cartState.AddItem(MakeProduct("B", 20000m));

        _cartState.TotalAmount.Should().Be(30000m);
    }
}
