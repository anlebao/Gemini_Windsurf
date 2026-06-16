using VanAn.CoreHub.Services.Orchestration;
using VanAn.Shared.Domain;
using VanAn.Integration.Tests.Infrastructure;
using Xunit;
using FluentAssertions;

namespace VanAn.Integration.Tests.Services;

/// <summary>
/// InvoicePolicyService integration tests — HKD business policy validation
/// Inherits IntegrationTestBase for SQLite in-memory infrastructure
/// </summary>
public class InvoicePolicyServiceTests : IntegrationTestBase
{
    private readonly InvoicePolicyService _sut;

    public InvoicePolicyServiceTests()
    {
        _sut = new InvoicePolicyService(_dbContext);
    }

    [Fact]
    public async Task ValidateInvoiceAsync_ExceedsAmountLimit_ThrowsException()
    {
        // Arrange: Create invoice exceeding 100B VND limit
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            150_000_000_000m, 15_000_000_000m, 165_000_000_000m, // 165B VND
            "Test Customer", "1234567890", "Test Address");

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = async () => await _sut.ValidateInvoiceAsync(invoice.InvoiceId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum allowed*");
    }

    [Fact]
    public async Task ValidateInvoiceAsync_WithinAmountLimit_Passes()
    {
        // Arrange: Create invoice within 100B VND limit
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            10_000_000m, 1_000_000m, 11_000_000m, // 11M VND
            "Test Customer", "1234567890", "Test Address");

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = async () => await _sut.ValidateInvoiceAsync(invoice.InvoiceId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CanSubmitAsync_ExceedsLimit_ReturnsFalse()
    {
        // Arrange: Create invoice exceeding limit
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            200_000_000_000m, 20_000_000_000m, 220_000_000_000m, // 220B VND
            "Test Customer", "1234567890", "Test Address");

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.CanSubmitAsync(invoice.InvoiceId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanSubmitAsync_WithinLimit_ReturnsTrue()
    {
        // Arrange: Create invoice within limit in Draft status
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            5_000_000m, 500_000m, 5_500_000m, // 5.5M VND
            "Test Customer", "1234567890", "Test Address");

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.CanSubmitAsync(invoice.InvoiceId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateInvoiceAsync_TaxApprovedStatus_ThrowsException()
    {
        // Arrange: Create invoice and transition to TaxApproved status
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            10_000_000m, 1_000_000m, 11_000_000m,
            "Test Customer", "1234567890", "Test Address");

        invoice.Submit(); // Draft -> PendingSend
        invoice.MarkAsSentToProvider(new ProviderId("viettel")); // PendingSend -> SentToProvider
        invoice.MarkAsTaxApproved("VT-001"); // SentToProvider -> TaxApproved

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = async () => await _sut.ValidateInvoiceAsync(invoice.InvoiceId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*TaxApproved*does not allow submission*");
    }

    [Fact]
    public async Task ValidateInvoiceAsync_VatMismatch_ThrowsException()
    {
        // Arrange: Create invoice with VAT that doesn't match calculated amount
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            10_000_000m, 5_000_000m, 15_000_000m, // VAT is 5M instead of expected 1M (10%)
            "Test Customer", "1234567890", "Test Address");

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = async () => await _sut.ValidateInvoiceAsync(invoice.InvoiceId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*VatAmount*does not match calculated*");
    }

    [Fact]
    public async Task CanSubmitAsync_MissingCustomerName_ReturnsFalse()
    {
        // Arrange: Create invoice with empty customer name
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            10_000_000m, 1_000_000m, 11_000_000m,
            "", "1234567890", "Test Address"); // Empty customer name

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.CanSubmitAsync(invoice.InvoiceId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanSubmitAsync_NonExistentInvoice_ReturnsFalse()
    {
        // Arrange: Non-existent invoice ID
        var fakeInvoiceId = new ElectronicInvoiceId(Guid.NewGuid());

        // Act
        var result = await _sut.CanSubmitAsync(fakeInvoiceId);

        // Assert
        result.Should().BeFalse();
    }
}
