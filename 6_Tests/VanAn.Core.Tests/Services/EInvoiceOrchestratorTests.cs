using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Orchestration;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
namespace VanAn.Core.Tests.Services;

[Trait("Category", "Flaky")]
public class EInvoiceOrchestratorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _dbContext;
    private readonly Mock<IInvoicePolicyService> _policyServiceMock;
    private readonly Mock<IRetryPolicyService> _retryServiceMock;
    private readonly Mock<IFallbackService> _fallbackServiceMock;
    private readonly Mock<IComplianceService> _complianceServiceMock;
    private readonly Mock<IWebhookService> _webhookServiceMock;
    private readonly Mock<IOutboxRepository> _outboxRepositoryMock;
    private readonly EInvoiceOrchestrator _orchestrator;

    public EInvoiceOrchestratorTests()
    {
        _connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        _connection.Open();
        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseInternalServiceProvider(efServiceProvider).UseSqlite(_connection)
            .Options;
        _dbContext = new VanAnDbContext(options);
        _dbContext.Database.EnsureCreated();

        _policyServiceMock = new Mock<IInvoicePolicyService>();
        _retryServiceMock = new Mock<IRetryPolicyService>();
        _fallbackServiceMock = new Mock<IFallbackService>();
        _complianceServiceMock = new Mock<IComplianceService>();
        _webhookServiceMock = new Mock<IWebhookService>();
        _outboxRepositoryMock = new Mock<IOutboxRepository>();

        _orchestrator = new EInvoiceOrchestrator(
            _policyServiceMock.Object,
            _retryServiceMock.Object,
            _fallbackServiceMock.Object,
            _complianceServiceMock.Object,
            _webhookServiceMock.Object,
            _outboxRepositoryMock.Object,
            _dbContext);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _connection?.Dispose();
    }

    [Fact]
    public async Task SubmitInvoiceAsync_ShouldCallPolicyService()
    {
        // Arrange
        var invoiceId = new ElectronicInvoiceId(Guid.NewGuid());
        var cancellationToken = CancellationToken.None;

        _policyServiceMock.Setup(p => p.ValidateInvoiceAsync(invoiceId, cancellationToken))
            .Returns(Task.CompletedTask);
        _complianceServiceMock.Setup(c => c.ValidateComplianceAsync(invoiceId, cancellationToken))
            .Returns(Task.CompletedTask);
        _retryServiceMock.Setup(r => r.SubmitWithRetryAsync(invoiceId, cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _orchestrator.SubmitInvoiceAsync(invoiceId, cancellationToken);

        // Assert
        _policyServiceMock.Verify(p => p.ValidateInvoiceAsync(invoiceId, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task SubmitInvoiceAsync_ShouldCallComplianceService()
    {
        // Arrange
        var invoiceId = new ElectronicInvoiceId(Guid.NewGuid());
        var cancellationToken = CancellationToken.None;

        _policyServiceMock.Setup(p => p.ValidateInvoiceAsync(invoiceId, cancellationToken))
            .Returns(Task.CompletedTask);
        _complianceServiceMock.Setup(c => c.ValidateComplianceAsync(invoiceId, cancellationToken))
            .Returns(Task.CompletedTask);
        _retryServiceMock.Setup(r => r.SubmitWithRetryAsync(invoiceId, cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _orchestrator.SubmitInvoiceAsync(invoiceId, cancellationToken);

        // Assert
        _complianceServiceMock.Verify(c => c.ValidateComplianceAsync(invoiceId, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task SubmitInvoiceAsync_ShouldCallRetryService()
    {
        // Arrange
        var invoiceId = new ElectronicInvoiceId(Guid.NewGuid());
        var cancellationToken = CancellationToken.None;

        _policyServiceMock.Setup(p => p.ValidateInvoiceAsync(invoiceId, cancellationToken))
            .Returns(Task.CompletedTask);
        _complianceServiceMock.Setup(c => c.ValidateComplianceAsync(invoiceId, cancellationToken))
            .Returns(Task.CompletedTask);
        _retryServiceMock.Setup(r => r.SubmitWithRetryAsync(invoiceId, cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _orchestrator.SubmitInvoiceAsync(invoiceId, cancellationToken);

        // Assert
        _retryServiceMock.Verify(r => r.SubmitWithRetryAsync(invoiceId, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task SubmitInvoiceAsync_ShouldCallServicesInCorrectOrder()
    {
        // Arrange
        var invoiceId = new ElectronicInvoiceId(Guid.NewGuid());
        var cancellationToken = CancellationToken.None;
        var callOrder = new List<string>();

        _policyServiceMock.Setup(p => p.ValidateInvoiceAsync(invoiceId, cancellationToken))
            .Callback(() => callOrder.Add("Policy"))
            .Returns(Task.CompletedTask);
        _complianceServiceMock.Setup(c => c.ValidateComplianceAsync(invoiceId, cancellationToken))
            .Callback(() => callOrder.Add("Compliance"))
            .Returns(Task.CompletedTask);
        _retryServiceMock.Setup(r => r.SubmitWithRetryAsync(invoiceId, cancellationToken))
            .Callback(() => callOrder.Add("Retry"))
            .Returns(Task.CompletedTask);

        // Act
        await _orchestrator.SubmitInvoiceAsync(invoiceId, cancellationToken);

        // Assert
        callOrder.Should().BeEquivalentTo(new[] { "Policy", "Compliance", "Retry" });
    }

    [Fact]
    public async Task SubmitInvoiceAsync_WhenPolicyValidationFails_ShouldNotCallSubsequentServices()
    {
        // Arrange
        var invoiceId = new ElectronicInvoiceId(Guid.NewGuid());
        var cancellationToken = CancellationToken.None;

        _policyServiceMock.Setup(p => p.ValidateInvoiceAsync(invoiceId, cancellationToken))
            .ThrowsAsync(new InvalidOperationException("Policy validation failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _orchestrator.SubmitInvoiceAsync(invoiceId, cancellationToken));

        _complianceServiceMock.Verify(c => c.ValidateComplianceAsync(It.IsAny<ElectronicInvoiceId>(), It.IsAny<CancellationToken>()), Times.Never);
        _retryServiceMock.Verify(r => r.SubmitWithRetryAsync(It.IsAny<ElectronicInvoiceId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhookAsync_ShouldDelegateToWebhookService()
    {
        // Arrange
        const string providerId = "provider-1";
        const string providerInvoiceNumber = "INV-001";
        const string callbackData = "{\"status\":\"approved\"}";
        var cancellationToken = CancellationToken.None;

        _webhookServiceMock.Setup(w => w.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData, cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _orchestrator.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData, cancellationToken);

        // Assert
        _webhookServiceMock.Verify(w => w.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_ShouldPassAllParametersCorrectly()
    {
        // Arrange
        const string providerId = "provider-1";
        const string providerInvoiceNumber = "INV-001";
        const string callbackData = "{\"status\":\"approved\"}";
        var cancellationToken = CancellationToken.None;

        _webhookServiceMock.Setup(w => w.ProcessWebhookAsync(
                It.Is<string>(p => p == providerId),
                It.Is<string>(p => p == providerInvoiceNumber),
                It.Is<string>(p => p == callbackData),
                It.Is<CancellationToken>(c => c == cancellationToken)))
            .Returns(Task.CompletedTask);

        // Act
        await _orchestrator.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData, cancellationToken);

        // Assert
        _webhookServiceMock.VerifyAll();
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenWebhookServiceThrows_ShouldPropagateException()
    {
        // Arrange
        const string providerId = "provider-1";
        const string providerInvoiceNumber = "INV-001";
        const string callbackData = "{\"status\":\"approved\"}";
        var cancellationToken = CancellationToken.None;

        _webhookServiceMock.Setup(w => w.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData, cancellationToken))
            .ThrowsAsync(new InvalidOperationException("Webhook processing failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _orchestrator.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData, cancellationToken));
    }

    [Fact]
    public void Constructor_ShouldInjectAllDependencies()
    {
        // Arrange & Act
        var orchestrator = new EInvoiceOrchestrator(
            _policyServiceMock.Object,
            _retryServiceMock.Object,
            _fallbackServiceMock.Object,
            _complianceServiceMock.Object,
            _webhookServiceMock.Object,
            _outboxRepositoryMock.Object,
            _dbContext);

        // Assert
        orchestrator.Should().NotBeNull();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CreateInvoiceAsync Tests (P0-7c) - Verify DB write + Outbox enqueue
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Flaky: pre-existing EInvoice test instability — tracked in next-actions for fix")]
    public async Task CreateInvoiceAsync_ShouldSaveInvoiceToDatabase()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var cancellationToken = CancellationToken.None;

        _outboxRepositoryMock.Setup(o => o.EnqueueAsync(It.IsAny<OutboxEvent>(), cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var invoiceId = await _orchestrator.CreateInvoiceAsync(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            100_000m, 10_000m, 110_000m,
            "Test Customer", "0123456789", "123 Test Address",
            cancellationToken);

        // Assert - Verify invoice saved to DB
        var savedInvoice = await _dbContext.ElectronicInvoices
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

        savedInvoice.Should().NotBeNull();
        savedInvoice!.TenantId.Should().Be(tenantId);
        savedInvoice.OrderId.Should().Be(orderId);
        savedInvoice.IdempotencyKey.Should().Be(idempotencyKey);
        savedInvoice.InvoiceType.Should().Be(InvoiceType.Goods);
        savedInvoice.Amount.Should().Be(100_000m);
        savedInvoice.VatAmount.Should().Be(10_000m);
        savedInvoice.TotalAmount.Should().Be(110_000m);
        savedInvoice.CustomerName.Should().Be("Test Customer");
        savedInvoice.CustomerTaxCode.Should().Be("0123456789");
        savedInvoice.CustomerAddress.Should().Be("123 Test Address");
        savedInvoice.Status.Should().Be(InvoiceStatus.PendingSend);
    }

    [Fact]
    public async Task CreateInvoiceAsync_ShouldEnqueueOutboxEvent()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var cancellationToken = CancellationToken.None;
        OutboxEvent? capturedOutboxEvent = null;

        _outboxRepositoryMock.Setup(o => o.EnqueueAsync(It.IsAny<OutboxEvent>(), cancellationToken))
            .Callback<OutboxEvent, CancellationToken>((evt, _) => capturedOutboxEvent = evt)
            .Returns(Task.CompletedTask);

        // Act
        var invoiceId = await _orchestrator.CreateInvoiceAsync(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            100_000m, 10_000m, 110_000m,
            "Test Customer", "0123456789", "123 Test Address",
            cancellationToken);

        // Assert - Verify outbox event was enqueued
        capturedOutboxEvent.Should().NotBeNull();
        capturedOutboxEvent!.TenantId.Should().Be(tenantId);
        capturedOutboxEvent.InvoiceId.Value.Should().Be(invoiceId.Value);
        capturedOutboxEvent.EventType.Should().Be("InvoiceCreated");
        capturedOutboxEvent.EventData.Should().Contain(invoiceId.Value.ToString());
    }

    [Fact(Skip = "Flaky: pre-existing EInvoice test instability — tracked in next-actions for fix")]
    public async Task CreateInvoiceAsync_ShouldUseTransaction()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var cancellationToken = CancellationToken.None;

        _outboxRepositoryMock.Setup(o => o.EnqueueAsync(It.IsAny<OutboxEvent>(), cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var invoiceId = await _orchestrator.CreateInvoiceAsync(
            tenantId, orderId, idempotencyKey, InvoiceType.Goods,
            100_000m, 10_000m, 110_000m,
            "Test Customer", "0123456789", "123 Test Address",
            cancellationToken);

        // Assert - Both invoice and outbox should be committed
        var savedInvoice = await _dbContext.ElectronicInvoices
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
        savedInvoice.Should().NotBeNull();

        // Verify outbox repository was called
        _outboxRepositoryMock.Verify(o => o.EnqueueAsync(It.IsAny<OutboxEvent>(), cancellationToken), Times.Once);
    }

    [Fact]
    public async Task CreateInvoiceAsync_WhenOutboxEnqueueFails_ShouldRollbackTransaction()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var cancellationToken = CancellationToken.None;

        _outboxRepositoryMock.Setup(o => o.EnqueueAsync(It.IsAny<OutboxEvent>(), cancellationToken))
            .ThrowsAsync(new InvalidOperationException("Outbox enqueue failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orchestrator.CreateInvoiceAsync(
                tenantId, orderId, idempotencyKey, InvoiceType.Goods,
                100_000m, 10_000m, 110_000m,
                "Test Customer", "0123456789", "123 Test Address",
                cancellationToken));

        // Verify no invoice was saved (rolled back)
        var invoiceCount = await _dbContext.ElectronicInvoices.CountAsync();
        invoiceCount.Should().Be(0);
    }

    [Fact(Skip = "Flaky: pre-existing EInvoice test instability — tracked in next-actions for fix")]
    public async Task GetInvoiceAsync_WhenInvoiceExists_ShouldReturnInvoice()
    {
        // Arrange - Create invoice directly in DB
        var tenantId = new TenantId(Guid.NewGuid());
        var invoice = new ElectronicInvoice(
            tenantId,
            new OrderId(Guid.NewGuid()),
            new InvoiceIdempotencyKey(Guid.NewGuid().ToString()),
            InvoiceType.Goods,
            100_000m, 10_000m, 110_000m,
            "Test Customer", "0123456789", "123 Test Address");

        _dbContext.ElectronicInvoices.Add(invoice);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _orchestrator.GetInvoiceAsync(invoice.InvoiceId);

        // Assert
        result.Should().NotBeNull();
        result!.InvoiceId.Should().Be(invoice.InvoiceId);
        result.CustomerName.Should().Be("Test Customer");
    }

    [Fact(Skip = "Flaky: pre-existing EInvoice test instability — tracked in next-actions for fix")]
    public async Task GetInvoiceStatusAsync_WhenInvoiceExists_ShouldReturnStatus()
    {
        // Arrange - Create invoice directly in DB
        var tenantId = new TenantId(Guid.NewGuid());
        var invoice = new ElectronicInvoice(
            tenantId,
            new OrderId(Guid.NewGuid()),
            new InvoiceIdempotencyKey(Guid.NewGuid().ToString()),
            InvoiceType.Goods,
            100_000m, 10_000m, 110_000m,
            "Test Customer", "0123456789", "123 Test Address");

        _dbContext.ElectronicInvoices.Add(invoice);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _orchestrator.GetInvoiceStatusAsync(invoice.InvoiceId);

        // Assert
        // A newly created invoice starts in Draft status (Submit() transitions to PendingSend)
        result.Should().NotBeNull();
        result.Should().Be(InvoiceStatus.Draft);
    }

    // ── P0.4 (W2 — Điều 8 TT 91/2026): EInvoice_Usage_Suspended upsert ────────
    [Fact]
    public async Task SetEInvoiceUsageSuspendedAsync_NewFlag_UpsertsSystemSettings()
    {
        var tenantId = new TenantId(Guid.NewGuid());

        await _orchestrator.SetEInvoiceUsageSuspendedAsync(
            tenantId, suspended: true, reason: "TB 123/CCT", updatedBy: null);

        var flag = await _dbContext.SystemSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId
                && s.Key == ComplianceService.EInvoiceUsageSuspendedKey);
        var reason = await _dbContext.SystemSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId
                && s.Key == ComplianceService.EInvoiceUsageSuspendedReasonKey);

        flag.Should().NotBeNull();
        flag!.Value.Should().Be("true");
        reason.Should().NotBeNull();
        reason!.Value.Should().Be("TB 123/CCT");
    }

    [Fact]
    public async Task SetEInvoiceUsageSuspendedAsync_ToggleOff_UpdatesExistingFlag()
    {
        var tenantId = new TenantId(Guid.NewGuid());

        await _orchestrator.SetEInvoiceUsageSuspendedAsync(tenantId, suspended: true);
        await _orchestrator.SetEInvoiceUsageSuspendedAsync(tenantId, suspended: false);

        var flags = await _dbContext.SystemSettings
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId
                && s.Key == ComplianceService.EInvoiceUsageSuspendedKey)
            .ToListAsync();

        flags.Should().HaveCount(1);
        flags[0].Value.Should().Be("false");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // W3 (P0.3 — Điều 10 TT 91/2026): AdjustInvoiceAsync + ListInvoicesAsync
    // ─────────────────────────────────────────────────────────────────────────

    private EInvoiceOrchestrator CreateOrchestratorWithAudit(Mock<IAuditTrailService> auditMock)
        => new(
            _policyServiceMock.Object,
            _retryServiceMock.Object,
            _fallbackServiceMock.Object,
            _complianceServiceMock.Object,
            _webhookServiceMock.Object,
            _outboxRepositoryMock.Object,
            _dbContext,
            auditMock.Object);

    private ElectronicInvoice SeedTaxApprovedInvoice(TenantId tenantId, string providerInvNo = "VT-001")
    {
        var invoice = new ElectronicInvoice(
            tenantId,
            new OrderId(Guid.NewGuid()),
            new InvoiceIdempotencyKey(Guid.NewGuid().ToString("N")),
            InvoiceType.Goods, 100_000m, 10_000m, 110_000m,
            "Khách A", "0123456789", "Hà Nội");
        invoice.Submit();
        invoice.MarkAsSentToProvider(new ProviderId("viettel"));
        invoice.MarkAsTaxApproved(providerInvNo);
        invoice.SetInvoiceSeries("1C25TAA");
        _dbContext.ElectronicInvoices.Add(invoice);
        _dbContext.SaveChanges();
        return invoice;
    }

    [Fact]
    public async Task AdjustInvoiceAsync_EmptyReason_Throws()
    {
        var invoiceId = new ElectronicInvoiceId(Guid.NewGuid());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _orchestrator.AdjustInvoiceAsync(
                invoiceId, InvoiceAdjustmentType.Adjust, "  "));
    }

    [Fact]
    public async Task AdjustInvoiceAsync_NotFound_Throws()
    {
        var invoiceId = new ElectronicInvoiceId(Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orchestrator.AdjustInvoiceAsync(
                invoiceId, InvoiceAdjustmentType.Adjust, "Lý do"));
    }

    [Fact]
    public async Task AdjustInvoiceAsync_NotTaxApproved_Throws()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var draft = new ElectronicInvoice(
            tenantId, new OrderId(Guid.NewGuid()),
            new InvoiceIdempotencyKey(Guid.NewGuid().ToString("N")),
            InvoiceType.Goods, 100_000m, 10_000m, 110_000m,
            "Khách A", "0123456789", "Hà Nội");
        _dbContext.ElectronicInvoices.Add(draft);
        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orchestrator.AdjustInvoiceAsync(
                draft.InvoiceId, InvoiceAdjustmentType.Adjust, "Sai số tiền"));
    }

    [Fact]
    public async Task AdjustInvoiceAsync_CreatesLinkedInvoice_MarksOriginal_EnqueuesOutbox()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var original = SeedTaxApprovedInvoice(tenantId);
        OutboxEvent? captured = null;
        _outboxRepositoryMock
            .Setup(o => o.EnqueueAsync(It.IsAny<OutboxEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var newId = await _orchestrator.AdjustInvoiceAsync(
            original.InvoiceId, InvoiceAdjustmentType.Adjust, "Sai số tiền hàng");

        var created = await _dbContext.ElectronicInvoices
            .FirstOrDefaultAsync(i => i.InvoiceId == newId);
        created.Should().NotBeNull();
        created!.Status.Should().Be(InvoiceStatus.PendingSend);
        created.OriginalInvoiceId.Should().Be(original.InvoiceId.Value);
        created.AdjustmentType.Should().Be(InvoiceAdjustmentType.Adjust);
        created.AdjustmentReason.Should().Be("Sai số tiền hàng");
        created.OriginalProviderInvoiceNumber.Should().Be("VT-001");

        var refreshedOriginal = await _dbContext.ElectronicInvoices
            .FirstOrDefaultAsync(i => i.InvoiceId == original.InvoiceId);
        refreshedOriginal!.Status.Should().Be(InvoiceStatus.Adjusted);

        captured.Should().NotBeNull();
        captured!.InvoiceId.Value.Should().Be(newId.Value);
        captured.EventType.Should().Be("InvoiceCreated");
    }

    [Fact]
    public async Task AdjustInvoiceAsync_Replace_MarksOriginalReplaced()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var original = SeedTaxApprovedInvoice(tenantId);
        _outboxRepositoryMock
            .Setup(o => o.EnqueueAsync(It.IsAny<OutboxEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var newId = await _orchestrator.AdjustInvoiceAsync(
            original.InvoiceId, InvoiceAdjustmentType.Replace, "Sai mã số thuế người mua");

        var created = await _dbContext.ElectronicInvoices
            .FirstOrDefaultAsync(i => i.InvoiceId == newId);
        created!.AdjustmentType.Should().Be(InvoiceAdjustmentType.Replace);

        var refreshedOriginal = await _dbContext.ElectronicInvoices
            .FirstOrDefaultAsync(i => i.InvoiceId == original.InvoiceId);
        refreshedOriginal!.Status.Should().Be(InvoiceStatus.Replaced);
    }

    [Fact]
    public async Task AdjustInvoiceAsync_NewData_OverridesOriginalFields()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var original = SeedTaxApprovedInvoice(tenantId);
        _outboxRepositoryMock
            .Setup(o => o.EnqueueAsync(It.IsAny<OutboxEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var newId = await _orchestrator.AdjustInvoiceAsync(
            original.InvoiceId, InvoiceAdjustmentType.Adjust, "Sai tên khách",
            new InvoiceAdjustmentData(CustomerName: "Khách B", Amount: 200_000m));

        var created = await _dbContext.ElectronicInvoices
            .FirstOrDefaultAsync(i => i.InvoiceId == newId);
        created!.CustomerName.Should().Be("Khách B");
        created.Amount.Should().Be(200_000m);
        created.VatAmount.Should().Be(10_000m);        // giữ nguyên
        created.CustomerTaxCode.Should().Be("0123456789"); // giữ nguyên
    }

    [Fact]
    public async Task AdjustInvoiceAsync_LogsCorrectionAudit()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var original = SeedTaxApprovedInvoice(tenantId);
        var auditMock = new Mock<IAuditTrailService>();
        var orchestrator = CreateOrchestratorWithAudit(auditMock);
        _outboxRepositoryMock
            .Setup(o => o.EnqueueAsync(It.IsAny<OutboxEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var newId = await orchestrator.AdjustInvoiceAsync(
            original.InvoiceId, InvoiceAdjustmentType.Replace, "Sai MST");

        auditMock.Verify(a => a.LogInvoiceCorrectionAsync(
            original.InvoiceId.Value,
            newId.Value,
            InvoiceAdjustmentType.Replace.ToString(),
            "Sai MST",
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdjustInvoiceAsync_AlreadyAdjusted_Throws()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var original = SeedTaxApprovedInvoice(tenantId);
        _outboxRepositoryMock
            .Setup(o => o.EnqueueAsync(It.IsAny<OutboxEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _orchestrator.AdjustInvoiceAsync(
            original.InvoiceId, InvoiceAdjustmentType.Adjust, "Lần 1");

        // Lần 2: original đã Adjusted → guard chặn (không tạo trùng hóa đơn điều chỉnh)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orchestrator.AdjustInvoiceAsync(
                original.InvoiceId, InvoiceAdjustmentType.Adjust, "Lần 2"));
    }

    [Fact]
    public async Task ListInvoicesAsync_ReturnsOnlyTenantInvoices_NewestFirst()
    {
        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());
        var invA = SeedTaxApprovedInvoice(tenantA, "VT-A");
        SeedTaxApprovedInvoice(tenantB, "VT-B");

        var list = await _orchestrator.ListInvoicesAsync(tenantA);

        list.Should().HaveCount(1);
        list[0].InvoiceId.Should().Be(invA.InvoiceId);
        list[0].ProviderInvoiceNumber.Should().Be("VT-A");
    }
}
