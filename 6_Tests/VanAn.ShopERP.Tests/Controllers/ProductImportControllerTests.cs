using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.DTOs;
using VanAn.ShopERP.Controllers;
using VanAn.ShopERP.Infrastructure;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using Xunit;

namespace VanAn.ShopERP.Tests.Controllers;

/// <summary>
/// OCR Onboarding S3 (2026-10-25): ProductImportController tests.
/// Dùng ShopERPDbContext thật (SQLite in-memory) cho tenant-exists check;
/// IProductService mock (kiểm tra CreateProductAsync được gọi đúng).
/// </summary>
public class ProductImportControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ShopERPDbContext _dbContext;

    public ProductImportControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ShopERPDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ShopERPDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private static ProductImportController CreateController(
        ShopERPDbContext dbContext,
        Mock<IProductService>? productServiceMock = null)
    {
        var service = productServiceMock ?? new Mock<IProductService>();
        service.Setup(s => s.CreateProductAsync(
                It.IsAny<CreateProductRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductDetailDto { ProductId = Guid.NewGuid(), Name = "ok" });
        return new ProductImportController(
            service.Object,
            dbContext,
            NullLogger<ProductImportController>.Instance);
    }

    private async Task<Guid> SeedTenantAsync(string name = "Shop Cà Phê ABC")
    {
        var tenantId = new TenantId(Guid.NewGuid());
        _dbContext.Tenants.Add(Tenant.CreateCompany(tenantId, name, null));
        await _dbContext.SaveChangesAsync();
        return tenantId.Value;
    }

    private static ProductImportRequest ImportRequest(Guid tenantId) => new()
    {
        TenantId = tenantId,
        Lines =
        [
            new ProductImportLine { Name = "Cà phê sữa đá", Price = 25000, LineNo = 1 },
            new ProductImportLine { Name = "Bánh mì trứng", Price = 20000, LineNo = 2 }
        ],
        DefaultCategory = "Menu"
    };

    [Fact(DisplayName = "Import_EmptyTenantId_Returns400")]
    public async Task Import_EmptyTenantId_Returns400()
    {
        var controller = CreateController(_dbContext);

        var result = await controller.Import(ImportRequest(Guid.Empty), CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>();
    }

    [Fact(DisplayName = "Import_EmptyLines_Returns400")]
    public async Task Import_EmptyLines_Returns400()
    {
        var controller = CreateController(_dbContext);
        var request = ImportRequest(Guid.NewGuid());
        request.Lines = [];

        var result = await controller.Import(request, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>();
    }

    [Fact(DisplayName = "Import_TenantNotSynced_Returns409")]
    public async Task Import_TenantNotSynced_Returns409()
    {
        var controller = CreateController(_dbContext);

        // Tenant chưa NATS sync vào SQLite mirror → Conflict
        var result = await controller.Import(ImportRequest(Guid.NewGuid()), CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>();
    }

    [Fact(DisplayName = "Import_WithSyncedTenant_CreatesProducts_WithMenuCategory")]
    public async Task Import_WithSyncedTenant_CreatesProducts_WithMenuCategory()
    {
        var productService = new Mock<IProductService>();
        var controller = CreateController(_dbContext, productService);
        var tenantId = await SeedTenantAsync();

        var result = await controller.Import(ImportRequest(tenantId), CancellationToken.None);

        var ok = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        var importResult = Assert.IsType<ProductImportResult>(ok.Value);
        importResult.Imported.Should().Be(2);
        importResult.Skipped.Should().Be(0);

        // Verify: CreateProductAsync được gọi với tenantId đúng + Category "Menu"
        productService.Verify(s => s.CreateProductAsync(
            It.Is<CreateProductRequest>(r =>
                r.Name == "Cà phê sữa đá" && r.Price == 25000 && r.Category == "Menu"),
            tenantId,
            It.IsAny<CancellationToken>()), Times.Once);
        productService.Verify(s => s.CreateProductAsync(
            It.Is<CreateProductRequest>(r =>
                r.Name == "Bánh mì trứng" && r.Price == 20000 && r.Category == "Menu"),
            tenantId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Import_BadLine_Skipped_DoesNotKillBatch")]
    public async Task Import_BadLine_Skipped_DoesNotKillBatch()
    {
        var productService = new Mock<IProductService>();
        var controller = CreateController(_dbContext, productService);
        // Dòng 2 (Bánh mì trứng) lỗi — mock throw (đặt SAU CreateController — Moq last-setup-wins);
        // dòng 1 vẫn import được
        productService.Setup(s => s.CreateProductAsync(
                It.Is<CreateProductRequest>(r => r.Name == "Bánh mì trứng"),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Tên trùng"));
        var tenantId = await SeedTenantAsync();

        var result = await controller.Import(ImportRequest(tenantId), CancellationToken.None);

        var ok = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        var importResult = Assert.IsType<ProductImportResult>(ok.Value);
        importResult.Imported.Should().Be(1);
        importResult.Skipped.Should().Be(1);
        importResult.Errors.Should().ContainSingle(e => e.LineNo == 2);
    }
}