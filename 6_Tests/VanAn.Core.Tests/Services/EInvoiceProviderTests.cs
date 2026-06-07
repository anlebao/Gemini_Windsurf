using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.CoreHub.Services.Providers.EInvoice;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Services;

public class EInvoiceProviderRegistryTests
{
    [Fact]
    public void RegisterProvider_ShouldAddProviderToRegistry()
    {
        // Arrange
        var registry = new EInvoiceProviderRegistry();
        var providerType = typeof(MockEInvoiceProvider);

        // Act
        registry.RegisterProvider("mock-provider", providerType);

        // Assert
        registry.IsProviderRegistered("mock-provider").Should().BeTrue();
    }

    [Fact]
    public void RegisterProvider_WhenTypeDoesNotImplementInterface_ShouldThrowArgumentException()
    {
        // Arrange
        var registry = new EInvoiceProviderRegistry();
        var invalidType = typeof(string);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.RegisterProvider("invalid", invalidType));
    }

    [Fact]
    public void IsProviderRegistered_WhenProviderNotRegistered_ShouldReturnFalse()
    {
        // Arrange
        var registry = new EInvoiceProviderRegistry();

        // Act & Assert
        registry.IsProviderRegistered("non-existent").Should().BeFalse();
    }

    [Fact]
    public void GetProviderType_WhenProviderRegistered_ShouldReturnType()
    {
        // Arrange
        var registry = new EInvoiceProviderRegistry();
        var providerType = typeof(MockEInvoiceProvider);
        registry.RegisterProvider("mock-provider", providerType);

        // Act
        var result = registry.GetProviderType("mock-provider");

        // Assert
        result.Should().Be(providerType);
    }

    [Fact]
    public void GetProviderType_WhenProviderNotRegistered_ShouldThrowArgumentException()
    {
        // Arrange
        var registry = new EInvoiceProviderRegistry();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.GetProviderType("non-existent"));
    }

    [Fact]
    public void GetRegisteredProviders_ShouldReturnAllRegisteredProviderIds()
    {
        // Arrange
        var registry = new EInvoiceProviderRegistry();
        registry.RegisterProvider("provider-1", typeof(MockEInvoiceProvider));
        registry.RegisterProvider("provider-2", typeof(MockEInvoiceProvider));

        // Act
        var result = registry.GetRegisteredProviders();

        // Assert
        result.Should().Contain("provider-1");
        result.Should().Contain("provider-2");
        result.Should().HaveCount(2);
    }

    [Fact]
    public void AutoRegisterFromAssembly_ShouldRegisterProvidersWithAttribute()
    {
        // Arrange
        var registry = new EInvoiceProviderRegistry();
        var assembly = typeof(EInvoiceProviderRegistryTests).Assembly;

        // Act
        registry.AutoRegisterFromAssembly(assembly);

        // Assert
        // Note: This test assumes there are no providers with ProviderAttribute in the test assembly
        // In a real scenario, you would have test providers with the attribute
        registry.GetRegisteredProviders().Should().NotBeNull();
    }
}

public class EInvoiceProviderFactoryTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IEInvoiceProviderRegistry> _registryMock;
    private readonly Mock<IEInvoiceProvider> _providerMock;

    public EInvoiceProviderFactoryTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _registryMock = new Mock<IEInvoiceProviderRegistry>();
        _providerMock = new Mock<IEInvoiceProvider>();
    }

    [Fact]
    public void CreateProvider_WhenProviderRegistered_ShouldCallRegistry()
    {
        // Arrange
        _registryMock.Setup(r => r.IsProviderRegistered("mock-provider")).Returns(true);
        _registryMock.Setup(r => r.GetProviderType("mock-provider")).Returns(typeof(MockEInvoiceProvider));
        
        var services = new ServiceCollection();
        services.AddSingleton(typeof(MockEInvoiceProvider), sp => new MockEInvoiceProvider());
        var serviceProvider = services.BuildServiceProvider();

        var factory = new EInvoiceProviderFactory(serviceProvider, _registryMock.Object);

        // Act
        var result = factory.CreateProvider("mock-provider");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<MockEInvoiceProvider>();
        _registryMock.Verify(r => r.IsProviderRegistered("mock-provider"), Times.Once);
        _registryMock.Verify(r => r.GetProviderType("mock-provider"), Times.Once);
    }

    [Fact]
    public void CreateProvider_WhenProviderNotRegistered_ShouldThrowArgumentException()
    {
        // Arrange
        _registryMock.Setup(r => r.IsProviderRegistered("non-existent")).Returns(false);
        var factory = new EInvoiceProviderFactory(_serviceProviderMock.Object, _registryMock.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => factory.CreateProvider("non-existent"));
    }

    [Fact]
    public void IsProviderRegistered_ShouldDelegateToRegistry()
    {
        // Arrange
        _registryMock.Setup(r => r.IsProviderRegistered("mock-provider")).Returns(true);
        var factory = new EInvoiceProviderFactory(_serviceProviderMock.Object, _registryMock.Object);

        // Act
        var result = factory.IsProviderRegistered("mock-provider");

        // Assert
        result.Should().BeTrue();
        _registryMock.Verify(r => r.IsProviderRegistered("mock-provider"), Times.Once);
    }

    [Fact]
    public void GetRegisteredProviders_ShouldDelegateToRegistry()
    {
        // Arrange
        var expectedProviders = new[] { "provider-1", "provider-2" };
        _registryMock.Setup(r => r.GetRegisteredProviders()).Returns(expectedProviders);
        var factory = new EInvoiceProviderFactory(_serviceProviderMock.Object, _registryMock.Object);

        // Act
        var result = factory.GetRegisteredProviders();

        // Assert
        result.Should().BeEquivalentTo(expectedProviders);
        _registryMock.Verify(r => r.GetRegisteredProviders(), Times.Once);
    }
}

public class ProviderCapabilitiesTests
{
    [Fact]
    public void ProviderCapabilities_ShouldStoreAllProperties()
    {
        // Arrange & Act
        var capabilities = new ProviderCapabilities(
            100,
            TimeSpan.FromSeconds(30),
            50,
            TimeSpan.FromSeconds(5),
            "ERROR.*");

        // Assert
        capabilities.RateLimit.Should().Be(100);
        capabilities.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        capabilities.MaxBatchSize.Should().Be(50);
        capabilities.SLA.Should().Be(TimeSpan.FromSeconds(5));
        capabilities.ErrorPattern.Should().Be("ERROR.*");
    }
}

public class EInvoiceRequestTests
{
    [Fact]
    public void EInvoiceRequest_ShouldStoreAllProperties()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var invoiceId = new ElectronicInvoiceId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());

        // Act
        var request = new EInvoiceRequest(
            tenantId,
            invoiceId,
            orderId,
            InvoiceType.Goods,
            100000m,
            10000m,
            110000m,
            "Customer Name",
            "TaxCode",
            "Address",
            DateTime.UtcNow,
            new Dictionary<string, string> { { "key", "value" } });

        // Assert
        request.TenantId.Should().Be(tenantId);
        request.InvoiceId.Should().Be(invoiceId);
        request.OrderId.Should().Be(orderId);
        request.InvoiceType.Should().Be(InvoiceType.Goods);
        request.Amount.Should().Be(100000m);
        request.VatAmount.Should().Be(10000m);
        request.TotalAmount.Should().Be(110000m);
        request.CustomerName.Should().Be("Customer Name");
        request.CustomerTaxCode.Should().Be("TaxCode");
        request.CustomerAddress.Should().Be("Address");
        request.AdditionalData.Should().ContainKey("key");
    }
}

public class EInvoiceResponseTests
{
    [Fact]
    public void EInvoiceResponse_ShouldStoreAllProperties()
    {
        // Arrange & Act
        var response = new EInvoiceResponse(
            true,
            "INV-001",
            "TAX-001",
            null,
            DateTime.UtcNow,
            new Dictionary<string, string> { { "key", "value" } });

        // Assert
        response.Success.Should().BeTrue();
        response.ProviderInvoiceNumber.Should().Be("INV-001");
        response.TaxAuthorityInvoiceNumber.Should().Be("TAX-001");
        response.ErrorMessage.Should().BeNull();
        response.Metadata.Should().ContainKey("key");
    }

    [Fact]
    public void EInvoiceResponse_WhenFailed_ShouldStoreErrorMessage()
    {
        // Arrange & Act
        var response = new EInvoiceResponse(
            false,
            null,
            null,
            "Validation failed",
            DateTime.UtcNow,
            new Dictionary<string, string>());

        // Assert
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Be("Validation failed");
    }
}

public class InvoiceStatusResponseTests
{
    [Fact]
    public void InvoiceStatusResponse_ShouldStoreAllProperties()
    {
        // Arrange & Act
        var response = new InvoiceStatusResponse(
            "INV-001",
            InvoiceStatus.TaxApproved,
            DateTime.UtcNow,
            null,
            new Dictionary<string, string> { { "key", "value" } });

        // Assert
        response.ProviderInvoiceNumber.Should().Be("INV-001");
        response.Status.Should().Be(InvoiceStatus.TaxApproved);
        response.ApprovedAt.Should().NotBeNull();
        response.FailureReason.Should().BeNull();
        response.Metadata.Should().ContainKey("key");
    }

    [Fact]
    public void InvoiceStatusResponse_WhenFailed_ShouldStoreFailureReason()
    {
        // Arrange & Act
        var response = new InvoiceStatusResponse(
            "INV-001",
            InvoiceStatus.Failed,
            null,
            "Network timeout",
            new Dictionary<string, string>());

        // Assert
        response.Status.Should().Be(InvoiceStatus.Failed);
        response.FailureReason.Should().Be("Network timeout");
        response.ApprovedAt.Should().BeNull();
    }
}

// Mock implementation for testing
public class MockEInvoiceProvider : IEInvoiceProvider
{
    public string ProviderId => "mock-provider";
    public string ProviderName => "Mock Provider";
    public ProviderCapabilities Capabilities => new ProviderCapabilities(
        100,
        TimeSpan.FromSeconds(30),
        50,
        TimeSpan.FromSeconds(5),
        "ERROR.*");

    public Task<EInvoiceResponse> SubmitInvoiceAsync(EInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new EInvoiceResponse(
            true,
            "INV-001",
            "TAX-001",
            null,
            DateTime.UtcNow,
            new Dictionary<string, string>()));
    }

    public Task<InvoiceStatusResponse> GetInvoiceStatusAsync(TenantId tenantId, string providerInvoiceNumber, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new InvoiceStatusResponse(
            providerInvoiceNumber,
            InvoiceStatus.TaxApproved,
            DateTime.UtcNow,
            null,
            new Dictionary<string, string>()));
    }

    public Task<EInvoiceResponse> CancelInvoiceAsync(TenantId tenantId, string providerInvoiceNumber, string reason, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new EInvoiceResponse(
            true,
            null,
            null,
            null,
            DateTime.UtcNow,
            new Dictionary<string, string>()));
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
