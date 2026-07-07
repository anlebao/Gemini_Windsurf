using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Infrastructure;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Integration.Tests;

/// <summary>
/// Bucket A feature (approved 2026-07-07): Integration tests for guest checkout endpoint
/// with customer info. Verifies the full flow: DTO binding → command → service → domain → DB persistence.
///
/// Uses GatewayWebApplicationFactory (SQLite in-memory, EnsureCreated schema).
/// Seeds a Product first (OrderItem has FK to Product). The controller hardcodes tenant
/// 00000000-0000-0000-0000-000000000001 (matches seeded product data per W4 fix comment).
/// Queries VanAnDbContext directly to verify Order.CustomerInfo (OwnsOne) persisted as columns.
/// </summary>
[Trait("Category", "Integration")]
public class GuestCheckoutEndpointTests : IClassFixture<GatewayWebApplicationFactory>
{
    private readonly GatewayWebApplicationFactory _factory;
    private readonly HttpClient _client;

    // Must match the tenant ID hardcoded in PublicOrdersController.CreateCheckoutOrder.
    private static readonly Guid CheckoutTenantId = new("00000000-0000-0000-0000-000000000001");

    public GuestCheckoutEndpointTests(GatewayWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<Guid> SeedProductAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        var tenantId = new TenantId(CheckoutTenantId);
        var product = new Product(tenantId, "Test Product", 50000m, "Test", costPrice: 30000m);
        _ = db.Products.Add(product);
        _ = await db.SaveChangesAsync();
        return product.Id;
    }

    [Fact(DisplayName = "Guest checkout: customer info persisted to Order.CustomerInfo")]
    public async Task Checkout_WithCustomerInfo_PersistsCustomerInfo()
    {
        // Arrange — seed a product so OrderItem FK is satisfied.
        // CustomerId is null for guest checkout (Bucket A fix: CustomerDeviceId ≠ CustomerId).
        Guid productId = await SeedProductAsync();

        var request = new
        {
            CustomerDeviceId = "test-device-1",
            OrderType = "TAKEAWAY",
            CustomerNotes = "Integration test order",
            CustomerName = "Nguyen Van A",
            CustomerPhone = "0901234567",
            CustomerAddress = "123 Le Loi, Q1, HCM",
            Items = new[]
            {
                new { ProductId = productId, Quantity = 2, UnitPrice = 50000m, Notes = "" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/public/orders/checkout", request);

        // Assert — endpoint returns 200 with OrderId
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CheckoutResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result!.OrderId);

        // Verify CustomerInfo persisted to DB (OwnsOne columns).
        // Use IgnoreQueryFilters because the order's TenantId (00000000-...001) differs
        // from TestTenantProvider's tenant (12345678-...abc) — the controller hardcodes the tenant.
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        var order = await db.Orders
            .Include(o => o.CustomerInfo)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == result.OrderId);

        Assert.NotNull(order);
        Assert.NotNull(order!.CustomerInfo);
        Assert.Equal("Nguyen Van A", order.CustomerInfo!.FullName);
        Assert.Equal("0901234567", order.CustomerInfo.PhoneNumber);
        Assert.Equal("123 Le Loi, Q1, HCM", order.CustomerInfo.Address);
    }

    [Fact(DisplayName = "Guest checkout: without customer info still works (backward compat)")]
    public async Task Checkout_WithoutCustomerInfo_StillSucceeds()
    {
        // Arrange — seed a product so OrderItem FK is satisfied
        Guid productId = await SeedProductAsync();

        var request = new
        {
            CustomerDeviceId = "test-device-2",
            OrderType = "TAKEAWAY",
            Items = new[]
            {
                new { ProductId = productId, Quantity = 1, UnitPrice = 25000m, Notes = "" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/public/orders/checkout", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CheckoutResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result!.OrderId);

        // CustomerInfo should be null (no customer info provided)
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        var order = await db.Orders
            .Include(o => o.CustomerInfo)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == result.OrderId);

        Assert.NotNull(order);
        // CustomerInfo is null when no customer info was provided (backward compat)
        Assert.Null(order!.CustomerInfo);
    }

    [Fact(DisplayName = "Guest checkout: empty items returns 400")]
    public async Task Checkout_WithEmptyItems_Returns400()
    {
        var request = new
        {
            CustomerDeviceId = "test-device-3",
            Items = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/api/public/orders/checkout", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class CheckoutResponse
    {
        public Guid OrderId { get; set; }
        public string? QrImageUrl { get; set; }
        public string? PaymentUrl { get; set; }
        public decimal Amount { get; set; }
    }
}
