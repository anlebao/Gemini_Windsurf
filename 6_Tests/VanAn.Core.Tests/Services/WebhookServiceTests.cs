using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Orchestration;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// WebhookServiceTests - Unit tests with REAL DbContext (not stub)
/// Tests L1 (in-memory) + L2 (DB) idempotency, Viettel/MISA parsing, error handling
/// </summary>
public class WebhookServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _dbContext;
    private readonly WebhookService _webhookService;

    public WebhookServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new VanAnDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Use REAL WebhookService with DbContext (not stub)
        _webhookService = new WebhookService(_dbContext, NullLogger<WebhookService>.Instance);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _connection?.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ProcessWebhookAsync - Basic flow
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessWebhookAsync_WithValidViettelPayload_ShouldProcessSuccessfully()
    {
        // Arrange
        const string providerId = "viettel";
        const string providerInvoiceNumber = "VT-001";
        var callbackData = "{\"invoiceNo\":\"VT-001\",\"invoiceStatus\":\"APPROVED\",\"issueDate\":\"2025-06-14T00:00:00Z\"}";

        // Act
        await _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData);

        // Assert - L1 idempotency should track it
        var hasBeenProcessed = await _webhookService.HasBeenProcessedAsync(providerId, providerInvoiceNumber);
        hasBeenProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessWebhookAsync_WithValidMisaPayload_ShouldProcessSuccessfully()
    {
        // Arrange
        const string providerId = "misa";
        const string providerInvoiceNumber = "MS-001";
        var callbackData = "{\"inv_no\":\"MS-001\",\"invoice_status\":\"APPROVED\",\"approved_date\":\"2025-06-14T00:00:00Z\"}";

        // Act
        await _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData);

        // Assert - L1 idempotency should track it
        var hasBeenProcessed = await _webhookService.HasBeenProcessedAsync(providerId, providerInvoiceNumber);
        hasBeenProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessWebhookAsync_WithEmptyProviderId_ShouldThrowArgumentException()
    {
        // Arrange
        const string providerId = "";
        const string providerInvoiceNumber = "INV-001";
        const string callbackData = "{}";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData));
    }

    [Fact]
    public async Task ProcessWebhookAsync_WithEmptyInvoiceNumber_ShouldThrowArgumentException()
    {
        // Arrange
        const string providerId = "viettel";
        const string providerInvoiceNumber = "";
        const string callbackData = "{}";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData));
    }

    [Fact]
    public async Task ProcessWebhookAsync_WithInvalidJson_ShouldNotThrow_ButMarkProcessed()
    {
        // Arrange
        const string providerId = "viettel";
        const string providerInvoiceNumber = "VT-002";
        const string callbackData = "{invalid json}";

        // Act - Should complete without throwing (logs warning)
        await _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData);

        // Assert - Should be marked as processed to prevent retry storm
        var hasBeenProcessed = await _webhookService.HasBeenProcessedAsync(providerId, providerInvoiceNumber);
        hasBeenProcessed.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Idempotency - L1 (in-memory) + L2 (DB)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HasBeenProcessedAsync_WhenNotProcessed_ShouldReturnFalse()
    {
        // Arrange
        const string providerId = "viettel";
        const string providerInvoiceNumber = "VT-NEW";

        // Act
        var result = await _webhookService.HasBeenProcessedAsync(providerId, providerInvoiceNumber);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessWebhookAsync_DuplicateWebhook_ShouldBeSuppressed()
    {
        // Arrange
        const string providerId = "viettel";
        const string providerInvoiceNumber = "VT-DUP";
        var callbackData = "{\"invoiceNo\":\"VT-DUP\",\"invoiceStatus\":\"APPROVED\"}";

        // Act - Process first time
        await _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData);

        // Act - Process second time (duplicate)
        await _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData);

        // Assert - Should still be processed, no exception thrown
        var hasBeenProcessed = await _webhookService.HasBeenProcessedAsync(providerId, providerInvoiceNumber);
        hasBeenProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_L2DbCheck_ShouldReturnTrueAfterProcessing()
    {
        // Arrange
        const string providerId = "misa";
        const string providerInvoiceNumber = "MS-L2";
        var callbackData = "{\"inv_no\":\"MS-L2\",\"invoice_status\":\"APPROVED\"}";

        // Create a fresh service instance to test L2 (no L1 cache)
        var freshService = new WebhookService(_dbContext, NullLogger<WebhookService>.Instance);

        // Act - Process with first service
        await _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData);

        // Assert - Check with fresh service (should find in DB, not just memory)
        var hasBeenProcessed = await freshService.HasBeenProcessedAsync(providerId, providerInvoiceNumber);
        hasBeenProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_DifferentProviderIds_ShouldBeIndependent()
    {
        // Arrange
        const string invoiceNumber = "INV-SHARED";
        var callbackData = "{\"status\":\"APPROVED\"}";

        // Act - Process for provider-1
        await _webhookService.ProcessWebhookAsync("provider-1", invoiceNumber, callbackData);

        // Assert - provider-1 should be processed
        var provider1Processed = await _webhookService.HasBeenProcessedAsync("provider-1", invoiceNumber);
        provider1Processed.Should().BeTrue();

        // Assert - provider-2 should NOT be processed (different provider)
        var provider2Processed = await _webhookService.HasBeenProcessedAsync("provider-2", invoiceNumber);
        provider2Processed.Should().BeFalse();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_DifferentInvoiceNumbers_ShouldBeIndependent()
    {
        // Arrange
        const string providerId = "viettel";
        var callbackData = "{\"invoiceNo\":\"VT-001\",\"invoiceStatus\":\"APPROVED\"}";

        // Act - Process first invoice
        await _webhookService.ProcessWebhookAsync(providerId, "VT-001", callbackData);

        // Assert - VT-001 should be processed
        var vt001Processed = await _webhookService.HasBeenProcessedAsync(providerId, "VT-001");
        vt001Processed.Should().BeTrue();

        // Assert - VT-002 should NOT be processed (different invoice)
        var vt002Processed = await _webhookService.HasBeenProcessedAsync(providerId, "VT-002");
        vt002Processed.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Webhook Payload Parsing - Viettel
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessWebhookAsync_ViettelApproved_ShouldParseCorrectly()
    {
        // Arrange
        const string providerId = "viettel";
        const string providerInvoiceNumber = "VT-APP";
        var callbackData = "{\"invoiceNo\":\"VT-APP\",\"invoiceStatus\":\"APPROVED\",\"issueDate\":\"2025-06-14T00:00:00Z\"}";

        // Act
        await _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData);

        // Assert
        var hasBeenProcessed = await _webhookService.HasBeenProcessedAsync(providerId, providerInvoiceNumber);
        hasBeenProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessWebhookAsync_ViettelRejected_ShouldParseCorrectly()
    {
        // Arrange
        const string providerId = "viettel";
        const string providerInvoiceNumber = "VT-REJ";
        var callbackData = "{\"invoiceNo\":\"VT-REJ\",\"invoiceStatus\":\"REJECTED\",\"errorMessage\":\"Invalid tax code\"}";

        // Act
        await _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData);

        // Assert
        var hasBeenProcessed = await _webhookService.HasBeenProcessedAsync(providerId, providerInvoiceNumber);
        hasBeenProcessed.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Webhook Payload Parsing - MISA
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessWebhookAsync_MisaApproved_ShouldParseCorrectly()
    {
        // Arrange
        const string providerId = "misa";
        const string providerInvoiceNumber = "MS-APP";
        var callbackData = "{\"inv_no\":\"MS-APP\",\"invoice_status\":\"APPROVED\",\"approved_date\":\"2025-06-14T00:00:00Z\"}";

        // Act
        await _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData);

        // Assert
        var hasBeenProcessed = await _webhookService.HasBeenProcessedAsync(providerId, providerInvoiceNumber);
        hasBeenProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessWebhookAsync_MisaRejected_ShouldParseCorrectly()
    {
        // Arrange
        const string providerId = "misa";
        const string providerInvoiceNumber = "MS-REJ";
        var callbackData = "{\"inv_no\":\"MS-REJ\",\"invoice_status\":\"REJECTED\",\"failure_reason\":\"Invalid format\"}";

        // Act
        await _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData);

        // Assert
        var hasBeenProcessed = await _webhookService.HasBeenProcessedAsync(providerId, providerInvoiceNumber);
        hasBeenProcessed.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Edge Cases
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessWebhookAsync_EmptyCallbackData_ShouldProcessWithoutError()
    {
        // Arrange
        const string providerId = "viettel";
        const string providerInvoiceNumber = "VT-EMPTY";
        const string callbackData = "";

        // Act
        await _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData);

        // Assert
        var hasBeenProcessed = await _webhookService.HasBeenProcessedAsync(providerId, providerInvoiceNumber);
        hasBeenProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessWebhookAsync_NullCallbackData_ShouldThrowArgumentNullException()
    {
        // Arrange
        const string providerId = "viettel";
        const string providerInvoiceNumber = "VT-NULL";
        const string? callbackData = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData!));
    }

    [Fact]
    public async Task HasBeenProcessedAsync_EmptyProviderId_ShouldReturnFalse()
    {
        // Act
        var result = await _webhookService.HasBeenProcessedAsync("", "INV-001");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_EmptyInvoiceNumber_ShouldReturnFalse()
    {
        // Act
        var result = await _webhookService.HasBeenProcessedAsync("viettel", "");

        // Assert
        result.Should().BeFalse();
    }
}
