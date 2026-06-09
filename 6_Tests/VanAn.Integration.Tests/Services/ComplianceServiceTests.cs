using VanAn.CoreHub.Services.Orchestration;
using VanAn.Shared.Domain;
using VanAn.Integration.Tests.Infrastructure;
using Xunit;
using FluentAssertions;

namespace VanAn.Integration.Tests.Services;

/// <summary>
/// ComplianceService integration tests — TT152-2025 structural validation
/// Inherits IntegrationTestBase for SQLite in-memory infrastructure
/// </summary>
public class ComplianceServiceTests : IntegrationTestBase
{
    private readonly ComplianceService _sut;

    public ComplianceServiceTests()
    {
        _sut = new ComplianceService(_dbContext);
    }

    [Fact]
    public async Task ValidateComplianceAsync_InvalidTaxCode_ThrowsValidationException()
    {
        // Arrange: Create invoice with invalid (short) tax code
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            100000m, 10000m, 110000m,
            "Test Customer", "12345", "Test Address"); // Tax code only 5 chars

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = async () => await _sut.ValidateComplianceAsync(invoice.InvoiceId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*CustomerTaxCode must be at least*");
    }

    [Fact]
    public async Task ValidateComplianceAsync_ValidInvoice_Passes()
    {
        // Arrange: Create valid invoice
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            100000m, 10000m, 110000m,
            "Test Customer", "1234567890", "123 Test Street, District 1"); // Valid 10-char tax code

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = async () => await _sut.ValidateComplianceAsync(invoice.InvoiceId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IsCompliantAsync_InvalidInvoice_ReturnsFalse()
    {
        // Arrange: Create invoice with missing customer name
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            100000m, 10000m, 110000m,
            "", "1234567890", "Test Address"); // Empty customer name

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.IsCompliantAsync(invoice.InvoiceId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsCompliantAsync_ValidInvoice_ReturnsTrue()
    {
        // Arrange: Create valid invoice
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            100000m, 10000m, 110000m,
            "Test Customer", "1234567890123", "456 Sample Road, Hanoi"); // Valid 13-char tax code

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.IsCompliantAsync(invoice.InvoiceId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateComplianceAsync_MissingAddress_ThrowsException()
    {
        // Arrange: Create invoice with empty address
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            100000m, 10000m, 110000m,
            "Test Customer", "1234567890", ""); // Empty address

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = async () => await _sut.ValidateComplianceAsync(invoice.InvoiceId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*CustomerAddress is required*");
    }

    [Fact]
    public async Task ValidateComplianceAsync_ZeroAmount_ThrowsException()
    {
        // Arrange: Create invoice with zero total amount
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            0m, 0m, 0m, // Zero amounts
            "Test Customer", "1234567890", "Test Address");

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = async () => await _sut.ValidateComplianceAsync(invoice.InvoiceId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TotalAmount must be greater*");
    }

    [Fact]
    public async Task IsCompliantAsync_NonExistentInvoice_ReturnsFalse()
    {
        // Arrange: Non-existent invoice ID
        var fakeInvoiceId = new ElectronicInvoiceId(Guid.NewGuid());

        // Act
        var result = await _sut.IsCompliantAsync(fakeInvoiceId);

        // Assert
        result.Should().BeFalse();
    }
}
