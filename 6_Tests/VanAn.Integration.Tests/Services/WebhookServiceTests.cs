using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Services.Orchestration;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;
using VanAn.Integration.Tests.Infrastructure;
using Xunit;
using FluentAssertions;

namespace VanAn.Integration.Tests.Services;

/// <summary>
/// WebhookService integration tests — Typed DTO parsing with Viettel/MISA contracts
/// Inherits IntegrationTestBase for SQLite in-memory infrastructure
/// </summary>
public class WebhookServiceTests : IntegrationTestBase
{
    private readonly WebhookService _sut;

    public WebhookServiceTests()
    {
        _sut = new WebhookService(_dbContext, NullLogger<WebhookService>.Instance);
    }

    [Fact]
    public async Task ProcessWebhookAsync_ViettelApprovedPayload_UpdatesInvoiceStatus()
    {
        // Arrange: Create invoice in SentToProvider status
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            10_000_000m, 1_000_000m, 11_000_000m,
            "Test Customer", "1234567890", "Test Address");

        invoice.Submit();
        invoice.MarkAsSentToProvider(new ProviderId("viettel"));

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        var providerInvoiceNumber = "VT-001";
        // Use reflection to set protected property for testing
        typeof(ElectronicInvoice).GetProperty("ProviderInvoiceNumber", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.SetValue(invoice, providerInvoiceNumber);
        await _dbContext.SaveChangesAsync();

        var payload = JsonSerializer.Serialize(new ViettelWebhookDto
        {
            InvoiceNo = providerInvoiceNumber,
            Status = 3, // Approved
            IssueDate = "2026-06-09",
            BuyerTaxCode = "1234567890",
            TotalAmount = 11_000_000m,
            TaxAmount = 1_000_000m
        });

        // Act
        await _sut.ProcessWebhookAsync("viettel", providerInvoiceNumber, payload);

        // Assert: Refresh entity from DB
        _dbContext.Entry(invoice).Reload();
        invoice.Status.Should().Be(InvoiceStatus.TaxApproved);
    }

    [Fact]
    public async Task ProcessWebhookAsync_MisaApprovedPayload_UpdatesInvoiceStatus()
    {
        // Arrange: Create invoice in SentToProvider status
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            10_000_000m, 1_000_000m, 11_000_000m,
            "Test Customer", "1234567890", "Test Address");

        invoice.Submit();
        invoice.MarkAsSentToProvider(new ProviderId("misa"));

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        var providerInvoiceNumber = "MS-001";
        // Use reflection to set protected property for testing
        typeof(ElectronicInvoice).GetProperty("ProviderInvoiceNumber", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.SetValue(invoice, providerInvoiceNumber);
        await _dbContext.SaveChangesAsync();

        var payload = JsonSerializer.Serialize(new MisaWebhookDto
        {
            TransactionId = Guid.NewGuid().ToString(),
            InvoiceCode = "MS-CODE",
            InvoiceNo = providerInvoiceNumber,
            ProcessStatus = 1, // Success
            ResultCode = 200,
            ResultMessage = "Approved",
            SubmitDate = DateTime.UtcNow.ToString("O")
        });

        // Act
        await _sut.ProcessWebhookAsync("misa", providerInvoiceNumber, payload);

        // Assert: Refresh entity from DB
        _dbContext.Entry(invoice).Reload();
        invoice.Status.Should().Be(InvoiceStatus.TaxApproved);
    }

    [Fact]
    public async Task ProcessWebhookAsync_ViettelRejectedPayload_MarksInvoiceRejected()
    {
        // Arrange: Create invoice in SentToProvider status
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            10_000_000m, 1_000_000m, 11_000_000m,
            "Test Customer", "1234567890", "Test Address");

        invoice.Submit();
        invoice.MarkAsSentToProvider(new ProviderId("viettel"));

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        var providerInvoiceNumber = "VT-002";
        // Use reflection to set protected property for testing
        typeof(ElectronicInvoice).GetProperty("ProviderInvoiceNumber", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.SetValue(invoice, providerInvoiceNumber);
        await _dbContext.SaveChangesAsync();

        var payload = JsonSerializer.Serialize(new ViettelWebhookDto
        {
            InvoiceNo = providerInvoiceNumber,
            Status = 4, // Rejected
            IssueDate = "2026-06-09",
            ErrorCode = "ERR-001",
            ErrorMessage = "Invalid tax code format"
        });

        // Act
        await _sut.ProcessWebhookAsync("viettel", providerInvoiceNumber, payload);

        // Assert: Refresh entity from DB
        _dbContext.Entry(invoice).Reload();
        invoice.Status.Should().Be(InvoiceStatus.Rejected);
    }

    [Fact]
    public async Task ProcessWebhookAsync_InvalidJson_LogsWarning_DoesNotCrash()
    {
        // Arrange
        var invalidPayload = "{ invalid json }";

        // Act: Should not throw
        var act = async () => await _sut.ProcessWebhookAsync("viettel", "INV-001", invalidPayload);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessWebhookAsync_DuplicateWebhook_IsIdempotent()
    {
        // Arrange: Create invoice
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            10_000_000m, 1_000_000m, 11_000_000m,
            "Test Customer", "1234567890", "Test Address");

        invoice.Submit();
        invoice.MarkAsSentToProvider(new ProviderId("viettel"));

        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        var providerInvoiceNumber = "VT-003";
        // Use reflection to set protected property for testing
        typeof(ElectronicInvoice).GetProperty("ProviderInvoiceNumber", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.SetValue(invoice, providerInvoiceNumber);
        await _dbContext.SaveChangesAsync();

        var payload = JsonSerializer.Serialize(new ViettelWebhookDto
        {
            InvoiceNo = providerInvoiceNumber,
            Status = 3, // Approved
            IssueDate = "2026-06-09"
        });

        // Act: Process same webhook twice
        await _sut.ProcessWebhookAsync("viettel", providerInvoiceNumber, payload);
        await _sut.ProcessWebhookAsync("viettel", providerInvoiceNumber, payload); // Duplicate

        // Assert: Second call should be suppressed (no exception)
        _dbContext.Entry(invoice).Reload();
        invoice.Status.Should().Be(InvoiceStatus.TaxApproved);
    }

    [Fact]
    public async Task ProcessWebhookAsync_NonExistentInvoice_LogsWarning()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new ViettelWebhookDto
        {
            InvoiceNo = "NON-EXISTENT",
            Status = 3
        });

        // Act: Should not throw, just log warning
        var act = async () => await _sut.ProcessWebhookAsync("viettel", "NON-EXISTENT", payload);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_NewWebhook_ReturnsFalse()
    {
        // Act
        var result = await _sut.HasBeenProcessedAsync("viettel", "NEW-001");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_AfterProcessing_ReturnsTrue()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new ViettelWebhookDto
        {
            InvoiceNo = "PROC-001",
            Status = 3
        });

        await _sut.ProcessWebhookAsync("viettel", "PROC-001", payload);

        // Act
        var result = await _sut.HasBeenProcessedAsync("viettel", "PROC-001");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessWebhookAsync_EmptyProviderId_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _sut.ProcessWebhookAsync("", "INV-001", "{}");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ProcessWebhookAsync_EmptyInvoiceNumber_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _sut.ProcessWebhookAsync("viettel", "", "{}");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_AfterRestart_NewInstance_ReturnsTrueFromDB()
    {
        // Arrange: process via sut1 (writes to DB)
        var sut1 = new WebhookService(_dbContext, NullLogger<WebhookService>.Instance);
        var payload = JsonSerializer.Serialize(new ViettelWebhookDto
        {
            InvoiceNo = "DURABLE-001",
            Status = 3
        });
        await sut1.ProcessWebhookAsync("viettel", "DURABLE-001", payload);

        // Act: new instance = no L1 cache, must fall through to DB
        var sut2 = new WebhookService(_dbContext, NullLogger<WebhookService>.Instance);
        var result = await sut2.HasBeenProcessedAsync("viettel", "DURABLE-001");

        // Assert: L2 DB lookup returns true
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessWebhookAsync_NewInstance_DuplicateKey_SuppressedViaDB()
    {
        // Arrange: create invoice, process Approved via sut1
        var tenantId = TestTenantId;
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            10_000_000m, 1_000_000m, 11_000_000m,
            "Test Customer", "1234567890", "Test Address");

        invoice.Submit();
        invoice.MarkAsSentToProvider(new ProviderId("viettel"));
        await _dbContext.ElectronicInvoices.AddAsync(invoice);
        await _dbContext.SaveChangesAsync();

        var providerInvoiceNumber = "DURABLE-002";
        typeof(ElectronicInvoice).GetProperty("ProviderInvoiceNumber", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.SetValue(invoice, providerInvoiceNumber);
        await _dbContext.SaveChangesAsync();

        var approvedPayload = JsonSerializer.Serialize(new ViettelWebhookDto
        {
            InvoiceNo = providerInvoiceNumber,
            Status = 3 // Approved
        });

        var sut1 = new WebhookService(_dbContext, NullLogger<WebhookService>.Instance);
        await sut1.ProcessWebhookAsync("viettel", providerInvoiceNumber, approvedPayload);

        _dbContext.Entry(invoice).Reload();
        invoice.Status.Should().Be(InvoiceStatus.TaxApproved);

        // Act: sut2 (new instance) attempts to process Rejected for same key
        var rejectedPayload = JsonSerializer.Serialize(new ViettelWebhookDto
        {
            InvoiceNo = providerInvoiceNumber,
            Status = 4, // Rejected
            ErrorMessage = "Duplicate attempt"
        });

        var sut2 = new WebhookService(_dbContext, NullLogger<WebhookService>.Instance);
        await sut2.ProcessWebhookAsync("viettel", providerInvoiceNumber, rejectedPayload);

        // Assert: status unchanged — Rejected payload was suppressed by DB idempotency
        _dbContext.Entry(invoice).Reload();
        invoice.Status.Should().Be(InvoiceStatus.TaxApproved);
    }
}
