using Moq;
using VanAn.CoreHub.Services.Orchestration;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Services;

public class EInvoiceOrchestratorTests
{
    private readonly Mock<IInvoicePolicyService> _policyServiceMock;
    private readonly Mock<IRetryPolicyService> _retryServiceMock;
    private readonly Mock<IFallbackService> _fallbackServiceMock;
    private readonly Mock<IComplianceService> _complianceServiceMock;
    private readonly Mock<IWebhookService> _webhookServiceMock;
    private readonly EInvoiceOrchestrator _orchestrator;

    public EInvoiceOrchestratorTests()
    {
        _policyServiceMock = new Mock<IInvoicePolicyService>();
        _retryServiceMock = new Mock<IRetryPolicyService>();
        _fallbackServiceMock = new Mock<IFallbackService>();
        _complianceServiceMock = new Mock<IComplianceService>();
        _webhookServiceMock = new Mock<IWebhookService>();

        _orchestrator = new EInvoiceOrchestrator(
            _policyServiceMock.Object,
            _retryServiceMock.Object,
            _fallbackServiceMock.Object,
            _complianceServiceMock.Object,
            _webhookServiceMock.Object);
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
            _webhookServiceMock.Object);

        // Assert
        orchestrator.Should().NotBeNull();
    }
}
