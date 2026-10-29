using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Services.Reconcile;
using VanAn.Shared.Domain;
using VanAn.ShopERP.Controllers;
using VanAn.ShopERP.Filters;
using VanAn.ShopERP.Infrastructure;
using Xunit;

namespace VanAn.ShopERP.Tests.Controllers;

/// <summary>
/// Tra soát P2 — MirrorOrdersController (internal API cho Gateway đọc SQLite mirror).
/// ShopERPDbContext thật (SQLite in-memory) — verify period filter CreatedAt + DTO map
/// + 400 period + auth InternalApiKey (401 missing / pass valid).
/// </summary>
public class MirrorOrdersControllerTests : IDisposable
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-0000000000CD"));
    private readonly SqliteConnection _connection;
    private readonly ShopERPDbContext _db;

    public MirrorOrdersControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ShopERPDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new ShopERPDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static MirrorOrdersController CreateController(ShopERPDbContext db)
        => new(db, NullLogger<MirrorOrdersController>.Instance);

    private async Task<Guid> SeedOrderAsync(DateTime createdAt, string status = "confirmed",
        string? sourceType = null, decimal deposit = 0m, string paymentStatus = "Paid")
    {
        Guid orderId = Guid.NewGuid();
        // OrderItem.Product navigation → EF convention FK ProductId → Products.Id — seed Product trước.
        var product = new Product(Tenant, "Sản phẩm test", "Test", 100_000m, "Nhóm test");
        _db.Products.Add(product);
        var item = OrderItem.Create(Guid.NewGuid(), Tenant, orderId, product.Id, 1, 100_000m, product.Name);
        var order = Order.Create(orderId, Tenant, null, [item]);
        order.ConfirmPayment("txn", "Cash");
        if (deposit > 0) order.SetDepositPaidAmount(deposit);
        typeof(Order).GetProperty("PaymentStatus")!.SetValue(order, paymentStatus);
        typeof(Order).GetProperty("Status")!.SetValue(order, new OrderStatusId(status));
        typeof(Order).GetProperty("CreatedAt")!.SetValue(order, createdAt);
        if (sourceType is not null) typeof(Order).GetProperty("SourceType")!.SetValue(order, sourceType);
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return orderId;
    }

    [Fact]
    public async Task GetMirrorOrders_PeriodFilter_OnlyInPeriod()
    {
        Guid inPeriod = await SeedOrderAsync(new DateTime(2026, 10, 15));
        _ = await SeedOrderAsync(new DateTime(2026, 9, 20));   // trước kỳ
        _ = await SeedOrderAsync(new DateTime(2026, 11, 2));   // sau kỳ

        var result = await CreateController(_db).GetMirrorOrders("2026-10", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<MirrorOrderDto>>(ok.Value);
        list.Should().HaveCount(1);
        list[0].OrderId.Should().Be(inPeriod);
    }

    [Fact]
    public async Task GetMirrorOrders_DtoMapping_Correct()
    {
        Guid orderId = await SeedOrderAsync(new DateTime(2026, 10, 15),
            status: "confirmed", sourceType: "booking", deposit: 10_000m);

        var result = await CreateController(_db).GetMirrorOrders("2026-10", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<MirrorOrderDto>>(ok.Value);
        var dto = list.Single(d => d.OrderId == orderId);
        dto.Status.Should().Be("confirmed");
        dto.PaymentStatus.Should().Be("Paid");
        dto.SourceType.Should().Be("booking");
        dto.DepositPaidAmount.Should().Be(10_000m);
        dto.TotalAmount.Should().Be(110_000m); // 100k + 10% VAT
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad")]
    [InlineData("2026-13")]   // tháng 13 — TryParseExact rejects
    [InlineData("2026-1")]    // thiếu 0 pad
    public async Task GetMirrorOrders_BadPeriod_Returns400(string? period)
    {
        var result = await CreateController(_db).GetMirrorOrders(period, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Auth (InternalApiKey — caller Gateway) ─────────────────────────────

    private static AuthorizationFilterContext BuildFilterContext(string? headerKey, string? configKey)
    {
        var services = new ServiceCollection();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["InternalLoyalty:ApiKey"]).Returns(configKey);
        services.AddSingleton<IConfiguration>(configMock.Object);
        var provider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        if (headerKey is not null) httpContext.Request.Headers["X-Internal-Api-Key"] = headerKey;
        return new AuthorizationFilterContext(new ActionContext(httpContext, new(), new()), []);
    }

    [Fact]
    public async Task InternalApiKey_Missing_Returns401()
    {
        var attr = new InternalApiKeyAttribute();
        var ctx = BuildFilterContext(headerKey: null, configKey: "key-123");
        await attr.OnAuthorizationAsync(ctx);
        ctx.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task InternalApiKey_Valid_Passes()
    {
        var attr = new InternalApiKeyAttribute();
        var ctx = BuildFilterContext(headerKey: "key-123", configKey: "key-123");
        await attr.OnAuthorizationAsync(ctx);
        ctx.Result.Should().BeNull("valid key → request proceeds");
    }
}
