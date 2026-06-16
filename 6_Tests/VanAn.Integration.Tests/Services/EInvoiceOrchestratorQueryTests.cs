using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Services.Orchestration;
using VanAn.Shared.Domain;
using VanAn.Integration.Tests.Infrastructure;
using Xunit;
using FluentAssertions;

namespace VanAn.Integration.Tests.Services;

/// <summary>
/// EInvoiceOrchestrator query method tests — GetInvoiceAsync & GetInvoiceStatusAsync
/// Inherits IntegrationTestBase for SQLite in-memory infrastructure
/// </summary>
public class EInvoiceOrchestratorQueryTests : IntegrationTestBase
{
    private readonly EInvoiceOrchestrator _sut;

    public EInvoiceOrchestratorQueryTests()
    {
        var policyService = GetService<IInvoicePolicyService>();
        var complianceService = GetService<IComplianceService>();
        var webhookService = GetService<IWebhookService>();
        var outboxRepository = GetService<IOutboxRepository>();

        // Use Moq for services not under test to simplify
        var retryServiceMock = new Moq.Mock<IRetryPolicyService>();
        var fallbackServiceMock = new Moq.Mock<IFallbackService>();

        _sut = new EInvoiceOrchestrator(
            policyService,
            retryServiceMock.Object,
            fallbackServiceMock.Object,
            complianceService,
            webhookService,
            outboxRepository,
            _dbContext);
    }

    [Fact]
    public async Task GetInvoiceAsync_ExistingId_ReturnsInvoice()
    {
        // Arrange: Create invoice
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            10_000_000m, 1_000_000m, 11_000_000m,
            "Test Customer", "1234567890", "Test Address");

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetInvoiceAsync(invoice.InvoiceId);

        // Assert
        result.Should().NotBeNull();
        result!.InvoiceId.Should().Be(invoice.InvoiceId);
        result.CustomerName.Should().Be("Test Customer");
        result.TotalAmount.Should().Be(11_000_000m);
    }

    [Fact]
    public async Task GetInvoiceAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        var fakeInvoiceId = new ElectronicInvoiceId(Guid.NewGuid());

        // Act
        var result = await _sut.GetInvoiceAsync(fakeInvoiceId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetInvoiceAsync_EmptyId_ReturnsNull()
    {
        // Arrange
        var emptyInvoiceId = new ElectronicInvoiceId(Guid.Empty);

        // Act
        var result = await _sut.GetInvoiceAsync(emptyInvoiceId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetInvoiceAsync_NullId_ReturnsNull()
    {
        // Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
        var result = await _sut.GetInvoiceAsync(null!);
#pragma warning restore CS8625

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetInvoiceStatusAsync_ExistingId_ReturnsStatus()
    {
        // Arrange: Create invoice in TaxApproved status
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            10_000_000m, 1_000_000m, 11_000_000m,
            "Test Customer", "1234567890", "Test Address");

        invoice.Submit();
        invoice.MarkAsSentToProvider(new ProviderId("viettel"));
        invoice.MarkAsTaxApproved("VT-001");

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetInvoiceStatusAsync(invoice.InvoiceId);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(InvoiceStatus.TaxApproved);
    }

    [Fact]
    public async Task GetInvoiceStatusAsync_DraftStatus_ReturnsDraft()
    {
        // Arrange: Create invoice in Draft status
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            10_000_000m, 1_000_000m, 11_000_000m,
            "Test Customer", "1234567890", "Test Address");

        // Status is Draft by default

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetInvoiceStatusAsync(invoice.InvoiceId);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public async Task GetInvoiceStatusAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        var fakeInvoiceId = new ElectronicInvoiceId(Guid.NewGuid());

        // Act
        var result = await _sut.GetInvoiceStatusAsync(fakeInvoiceId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetInvoiceStatusAsync_EmptyId_ReturnsNull()
    {
        // Arrange
        var emptyInvoiceId = new ElectronicInvoiceId(Guid.Empty);

        // Act
        var result = await _sut.GetInvoiceStatusAsync(emptyInvoiceId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetInvoiceStatusAsync_NullId_ReturnsNull()
    {
        // Act
#pragma warning disable CS8625
        var result = await _sut.GetInvoiceStatusAsync(null!);
#pragma warning restore CS8625

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetInvoiceAsync_MultipleInvoices_ReturnsCorrectInvoice()
    {
        // Arrange: Create multiple invoices
        var tenantId = TestTenantId;

        var invoice1 = new ElectronicInvoice(
            tenantId, new OrderId(Guid.NewGuid()),
            new InvoiceIdempotencyKey(Guid.NewGuid().ToString()), InvoiceType.Goods,
            10_000_000m, 1_000_000m, 11_000_000m,
            "Customer A", "1234567890", "Address A");

        var invoice2 = new ElectronicInvoice(
            tenantId, new OrderId(Guid.NewGuid()),
            new InvoiceIdempotencyKey(Guid.NewGuid().ToString()), InvoiceType.Services,
            20_000_000m, 2_000_000m, 22_000_000m,
            "Customer B", "0987654321", "Address B");

        await _dbContext.ElectronicInvoices.AddRangeAsync(invoice1, invoice2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetInvoiceAsync(invoice2.InvoiceId);

        // Assert
        result.Should().NotBeNull();
        result!.CustomerName.Should().Be("Customer B");
        result.TotalAmount.Should().Be(22_000_000m);
    }
}
