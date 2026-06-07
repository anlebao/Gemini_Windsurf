using Microsoft.Extensions.DependencyInjection;

namespace VanAn.CoreHub.Services.Providers.EInvoice;

/// <summary>
/// EInvoiceProviderFactory - Factory implementation for E-Invoice providers
/// Uses IServiceProvider for dependency injection
/// </summary>
public class EInvoiceProviderFactory : IEInvoiceProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEInvoiceProviderRegistry _registry;

    public EInvoiceProviderFactory(
        IServiceProvider serviceProvider,
        IEInvoiceProviderRegistry registry)
    {
        _serviceProvider = serviceProvider;
        _registry = registry;
    }

    public IEInvoiceProvider CreateProvider(string providerId)
    {
        if (!_registry.IsProviderRegistered(providerId))
            throw new ArgumentException($"Provider '{providerId}' is not registered", nameof(providerId));

        var providerType = _registry.GetProviderType(providerId);
        return (IEInvoiceProvider)_serviceProvider.GetRequiredService(providerType);
    }

    public bool IsProviderRegistered(string providerId)
    {
        return _registry.IsProviderRegistered(providerId);
    }

    public IEnumerable<string> GetRegisteredProviders()
    {
        return _registry.GetRegisteredProviders();
    }
}
