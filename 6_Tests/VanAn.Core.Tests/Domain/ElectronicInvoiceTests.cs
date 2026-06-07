using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Domain;

public class ElectronicInvoiceTests
{
    private readonly TenantId _tenantId;
    private readonly OrderId _orderId;
    private readonly InvoiceIdempotencyKey _idempotencyKey;

    public ElectronicInvoiceTests()
    {
        _tenantId = new TenantId(Guid.NewGuid());
        _orderId = new OrderId(Guid.NewGuid());
        _idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
    }

    [Fact]
    public void Constructor_ShouldCreateInvoiceWithDraftStatus()
    {
        // Arrange & Act
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.InvoiceType.Should().Be(InvoiceType.Goods);
        invoice.Amount.Should().Be(100000m);
        invoice.VatAmount.Should().Be(10000m);
        invoice.TotalAmount.Should().Be(110000m);
        invoice.CustomerName.Should().Be("Customer Name");
        invoice.CustomerTaxCode.Should().Be("TaxCode");
        invoice.CustomerAddress.Should().Be("Address");
    }

    [Fact]
    public void Submit_ShouldTransitionFromDraftToPendingSend()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");

        // Act
        invoice.Submit();

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.PendingSend);
    }

    [Fact]
    public void Submit_WhenNotInDraftStatus_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        invoice.Submit(); // Move to PendingSend

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => invoice.Submit());
    }

    [Fact]
    public void MarkAsSentToProvider_ShouldTransitionFromPendingSendToSentToProvider()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        invoice.Submit();
        var providerId = new ProviderId("provider-1");

        // Act
        invoice.MarkAsSentToProvider(providerId);

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.SentToProvider);
        invoice.CurrentProvider.Should().Be(providerId);
        invoice.SubmittedAt.Should().NotBeNull();
        invoice.SubmittedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MarkAsSentToProvider_WhenNotInPendingSendStatus_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        var providerId = new ProviderId("provider-1");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => invoice.MarkAsSentToProvider(providerId));
    }

    [Fact]
    public void MarkAsTaxApproved_ShouldTransitionFromSentToProviderToTaxApproved()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        invoice.Submit();
        var providerId = new ProviderId("provider-1");
        invoice.MarkAsSentToProvider(providerId);
        const string providerInvoiceNumber = "INV-001";

        // Act
        invoice.MarkAsTaxApproved(providerInvoiceNumber);

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.TaxApproved);
        invoice.ProviderInvoiceNumber.Should().Be(providerInvoiceNumber);
        invoice.ApprovedAt.Should().NotBeNull();
        invoice.ApprovedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MarkAsTaxApproved_WhenNotInSentToProviderStatus_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        const string providerInvoiceNumber = "INV-001";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => invoice.MarkAsTaxApproved(providerInvoiceNumber));
    }

    [Fact]
    public void MarkAsFailed_ShouldTransitionFromSentToProviderToFailed()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        invoice.Submit();
        var providerId = new ProviderId("provider-1");
        invoice.MarkAsSentToProvider(providerId);
        const string failureReason = "Network timeout";

        // Act
        invoice.MarkAsFailed(failureReason);

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.Failed);
        invoice.FailureReason.Should().Be(failureReason);
    }

    [Fact]
    public void MarkAsFailed_WhenNotInSentToProviderStatus_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        const string failureReason = "Network timeout";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => invoice.MarkAsFailed(failureReason));
    }

    [Fact]
    public void MarkAsRejected_ShouldTransitionFromSentToProviderToRejected()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        invoice.Submit();
        var providerId = new ProviderId("provider-1");
        invoice.MarkAsSentToProvider(providerId);
        const string rejectionReason = "Invalid tax code";

        // Act
        invoice.MarkAsRejected(rejectionReason);

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.Rejected);
        invoice.FailureReason.Should().Be(rejectionReason);
    }

    [Fact]
    public void MarkAsRejected_WhenNotInSentToProviderStatus_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        const string rejectionReason = "Invalid tax code";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => invoice.MarkAsRejected(rejectionReason));
    }

    [Fact]
    public void FullStateTransition_ShouldAllowCompleteLifecycle()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        var providerId = new ProviderId("provider-1");

        // Act - Full lifecycle
        invoice.Submit();
        invoice.MarkAsSentToProvider(providerId);
        invoice.MarkAsTaxApproved("INV-001");

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.TaxApproved);
    }
}

public class InvoiceAggregateTests
{
    private readonly TenantId _tenantId;
    private readonly OrderId _orderId;
    private readonly InvoiceIdempotencyKey _idempotencyKey;

    public InvoiceAggregateTests()
    {
        _tenantId = new TenantId(Guid.NewGuid());
        _orderId = new OrderId(Guid.NewGuid());
        _idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
    }

    [Fact]
    public void Constructor_ShouldCreateAggregateWithInvoice()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");

        // Act
        var aggregate = new InvoiceAggregate(invoice);

        // Assert
        aggregate.Invoice.Should().Be(invoice);
        aggregate.InvoiceId.Should().Be(invoice.InvoiceId);
        aggregate.Status.Should().Be(invoice.Status);
    }

    [Fact]
    public void Submit_ShouldDelegateToInvoiceAndUpdateStatus()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        var aggregate = new InvoiceAggregate(invoice);

        // Act
        aggregate.Submit();

        // Assert
        aggregate.Status.Should().Be(InvoiceStatus.PendingSend);
        invoice.Status.Should().Be(InvoiceStatus.PendingSend);
    }

    [Fact]
    public void Submit_WhenNotInDraftStatus_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        var aggregate = new InvoiceAggregate(invoice);
        aggregate.Submit(); // Move to PendingSend

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => aggregate.Submit());
    }

    [Fact]
    public void MarkAsSentToProvider_ShouldDelegateToInvoiceAndUpdateStatus()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        var aggregate = new InvoiceAggregate(invoice);
        aggregate.Submit();
        var providerId = new ProviderId("provider-1");

        // Act
        aggregate.MarkAsSentToProvider(providerId);

        // Assert
        aggregate.Status.Should().Be(InvoiceStatus.SentToProvider);
        invoice.Status.Should().Be(InvoiceStatus.SentToProvider);
        invoice.CurrentProvider.Should().Be(providerId);
    }

    [Fact]
    public void MarkAsSentToProvider_WhenNotInPendingSendStatus_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        var aggregate = new InvoiceAggregate(invoice);
        var providerId = new ProviderId("provider-1");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => aggregate.MarkAsSentToProvider(providerId));
    }

    [Fact]
    public void MarkAsTaxApproved_ShouldDelegateToInvoiceAndUpdateStatus()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        var aggregate = new InvoiceAggregate(invoice);
        aggregate.Submit();
        var providerId = new ProviderId("provider-1");
        aggregate.MarkAsSentToProvider(providerId);
        const string providerInvoiceNumber = "INV-001";

        // Act
        aggregate.MarkAsTaxApproved(providerInvoiceNumber);

        // Assert
        aggregate.Status.Should().Be(InvoiceStatus.TaxApproved);
        invoice.Status.Should().Be(InvoiceStatus.TaxApproved);
        invoice.ProviderInvoiceNumber.Should().Be(providerInvoiceNumber);
    }

    [Fact]
    public void MarkAsTaxApproved_WhenNotInSentToProviderStatus_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        var aggregate = new InvoiceAggregate(invoice);
        const string providerInvoiceNumber = "INV-001";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => aggregate.MarkAsTaxApproved(providerInvoiceNumber));
    }

    [Fact]
    public void MarkAsFailed_ShouldDelegateToInvoiceAndUpdateStatus()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        var aggregate = new InvoiceAggregate(invoice);
        aggregate.Submit();
        var providerId = new ProviderId("provider-1");
        aggregate.MarkAsSentToProvider(providerId);
        const string failureReason = "Network timeout";

        // Act
        aggregate.MarkAsFailed(failureReason);

        // Assert
        aggregate.Status.Should().Be(InvoiceStatus.Failed);
        invoice.Status.Should().Be(InvoiceStatus.Failed);
        invoice.FailureReason.Should().Be(failureReason);
    }

    [Fact]
    public void MarkAsFailed_WhenNotInSentToProviderStatus_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        var aggregate = new InvoiceAggregate(invoice);
        const string failureReason = "Network timeout";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => aggregate.MarkAsFailed(failureReason));
    }

    [Fact]
    public void FullAggregateLifecycle_ShouldEnforceStateTransitions()
    {
        // Arrange
        var invoice = new ElectronicInvoice(
            _tenantId,
            _orderId,
            _idempotencyKey,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address");
        var aggregate = new InvoiceAggregate(invoice);
        var providerId = new ProviderId("provider-1");

        // Act - Full lifecycle through aggregate
        aggregate.Submit();
        aggregate.MarkAsSentToProvider(providerId);
        aggregate.MarkAsTaxApproved("INV-001");

        // Assert
        aggregate.Status.Should().Be(InvoiceStatus.TaxApproved);
        invoice.Status.Should().Be(InvoiceStatus.TaxApproved);
    }
}
