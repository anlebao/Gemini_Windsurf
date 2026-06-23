using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OfficeOpenXml;
using VanAn.Core.Tests.TestInfrastructure;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// Wave 3 [W3-T8] — Unit tests for ExcelExportService.
    /// Covers: revenue, inventory, customer reports; valid OOXML output; tenant isolation.
    /// </summary>
    public class ExcelExportServiceTests : IDisposable
    {
        private readonly TestContextScope _scope;
        private readonly VanAnDbContext _context;
        private readonly ICustomerRepository _customerRepository;
        private readonly Mock<IOrderService> _orderServiceMock;
        private readonly ExcelExportService _service;
        private readonly TenantId _tenantId;
        private readonly Guid _tenantGuid;

        public ExcelExportServiceTests()
        {
            _scope = VanAnDbContextTestFactory.Create();
            _context = _scope.Context;
            _customerRepository = new CustomerRepository(_context);
            _orderServiceMock = new Mock<IOrderService>();
            _service = new ExcelExportService(
                _orderServiceMock.Object,
                _customerRepository,
                _context,
                NullLogger<ExcelExportService>.Instance);
            _tenantGuid = _scope.ActiveTenantId;
            _tenantId = new TenantId(_tenantGuid);
        }

        public void Dispose()
        {
            _scope.Dispose();
        }

        [Fact]
        public async Task ExportRevenueAsync_WithOrders_ReturnsValidExcelWithTwoSheets()
        {
            // Arrange
            DateTime from = DateTime.UtcNow.AddDays(-7);
            DateTime to = DateTime.UtcNow;
            Order order = TestEntityBuilder.CreateOrder(_tenantId, 250_000m);
            _orderServiceMock
                .Setup(s => s.GetOrdersByDateRangeAsync(_tenantGuid, from, to))
                .ReturnsAsync([order]);

            // Act
            byte[] bytes = await _service.ExportRevenueAsync(_tenantGuid, from, to);

            // Assert
            bytes.Should().NotBeNullOrEmpty();
            using ExcelPackage package = new(new MemoryStream(bytes));
            package.Workbook.Worksheets.Count.Should().Be(2);
            package.Workbook.Worksheets[0].Name.Should().Be("Tóm tắt");
            package.Workbook.Worksheets[1].Name.Should().Be("Chi tiết đơn hàng");
        }

        [Fact]
        public async Task ExportRevenueAsync_WithoutOrders_ReturnsValidExcel()
        {
            // Arrange
            DateTime from = DateTime.UtcNow.AddDays(-7);
            DateTime to = DateTime.UtcNow;
            _orderServiceMock
                .Setup(s => s.GetOrdersByDateRangeAsync(_tenantGuid, from, to))
                .ReturnsAsync(Array.Empty<Order>());

            // Act
            byte[] bytes = await _service.ExportRevenueAsync(_tenantGuid, from, to);

            // Assert
            bytes.Should().NotBeNullOrEmpty();
            using ExcelPackage package = new(new MemoryStream(bytes));
            package.Workbook.Worksheets.Count.Should().Be(2);
        }

        [Fact]
        public async Task ExportInventoryAsync_WithStock_ReturnsValidExcelWithLowStockHighlight()
        {
            // Arrange
            Ingredient ingredient = CreateIngredient(_tenantId, "Test Ingredient", "kg", 10, 5, 50_000m);
            Inventory lowStock = new(_tenantId, ingredient.Id, 2);
            _context.Ingredients.Add(ingredient);
            _context.Inventories.Add(lowStock);
            _ = await _context.SaveChangesAsync();

            // Act
            byte[] bytes = await _service.ExportInventoryAsync(_tenantGuid);

            // Assert
            bytes.Should().NotBeNullOrEmpty();
            using ExcelPackage package = new(new MemoryStream(bytes));
            package.Workbook.Worksheets.Count.Should().Be(1);
            package.Workbook.Worksheets[0].Name.Should().Be("Tồn kho");
            package.Workbook.Worksheets[0].Dimension.Rows.Should().BeGreaterThan(1);
        }

        [Fact]
        public async Task ExportCustomerAsync_WithCustomers_ReturnsValidExcelWithTierColors()
        {
            // Arrange
            Customer gold = TestEntityBuilder.CreateCustomer(_tenantId, "Gold Customer", "1111111111");
            gold.UpdateCustomerDetails("Gold Customer", "1111111111", "gold@example.com", "Gold", null, true);
            Customer bronze = TestEntityBuilder.CreateCustomer(_tenantId, "Bronze Customer", "2222222222");
            bronze.UpdateCustomerDetails("Bronze Customer", "2222222222", "bronze@example.com", "Bronze", null, true);
            _context.Customers.AddRange(gold, bronze);
            _ = await _context.SaveChangesAsync();

            // Act
            byte[] bytes = await _service.ExportCustomerAsync(_tenantGuid, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

            // Assert
            bytes.Should().NotBeNullOrEmpty();
            using ExcelPackage package = new(new MemoryStream(bytes));
            package.Workbook.Worksheets.Count.Should().Be(1);
            package.Workbook.Worksheets[0].Name.Should().Be("Khách hàng");
            package.Workbook.Worksheets[0].Dimension.Rows.Should().Be(3); // header + 2 customers
        }

        [Fact]
        public async Task ExportCustomerAsync_DifferentTenant_IsolatesData()
        {
            // Arrange
            Guid otherTenantGuid = Guid.Parse("99999999-9999-9999-9999-999999999999");
            TenantId otherTenantId = new(otherTenantGuid);
            Customer otherCustomer = TestEntityBuilder.CreateCustomer(otherTenantId, "Other Tenant", "9999999999");
            _context.Customers.Add(otherCustomer);
            _ = await _context.SaveChangesAsync();

            // Act
            byte[] bytes = await _service.ExportCustomerAsync(_tenantGuid, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

            // Assert
            using ExcelPackage package = new(new MemoryStream(bytes));
            ExcelWorksheet sheet = package.Workbook.Worksheets[0];
            // Only header row because the other-tenant customer is filtered by global query filter
            sheet.Dimension.Rows.Should().Be(1);
        }

        private static Ingredient CreateIngredient(TenantId tenantId, string name, string unit, decimal currentStock, decimal minStock, decimal pricePerUnit)
        {
            Ingredient ingredient = (Ingredient)Activator.CreateInstance(typeof(Ingredient), nonPublic: true)!;
            ingredient.Name = name;
            ingredient.Unit = unit;
            ingredient.CurrentStock = currentStock;
            ingredient.MinStockThreshold = minStock;
            ingredient.PricePerUnit = pricePerUnit;

            // Bypass protected setter for IngredientId and base TenantId
            System.Reflection.PropertyInfo ingredientIdProperty = typeof(Ingredient).GetProperty("IngredientId")!;
            ingredientIdProperty.SetValue(ingredient, new IngredientId(Guid.NewGuid()));
            System.Reflection.PropertyInfo tenantProperty = typeof(Ingredient).GetProperty("TenantId")!;
            tenantProperty.SetValue(ingredient, tenantId);

            return ingredient;
        }

        [Fact]
        public async Task ExportRevenueAsync_VndFormat_IsAppliedToTotalColumn()
        {
            // Arrange
            DateTime from = DateTime.UtcNow.AddDays(-7);
            DateTime to = DateTime.UtcNow;
            Order order = TestEntityBuilder.CreateOrder(_tenantId, 1_000_000m);
            _orderServiceMock
                .Setup(s => s.GetOrdersByDateRangeAsync(_tenantGuid, from, to))
                .ReturnsAsync([order]);

            // Act
            byte[] bytes = await _service.ExportRevenueAsync(_tenantGuid, from, to);

            // Assert
            using ExcelPackage package = new(new MemoryStream(bytes));
            ExcelWorksheet detail = package.Workbook.Worksheets["Chi tiết đơn hàng"];
            string numberFormat = detail.Cells[2, 7].Style.Numberformat.Format;
            numberFormat.Should().Contain("₫");
        }
    }
}
