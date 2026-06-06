using Microsoft.Extensions.DependencyInjection;

namespace VanAn.CoreHub.Services.Providers.POS;

/// <summary>
/// POSProviderFactory - Factory implementation for POS providers
/// Uses IServiceProvider for dependency injection
/// </summary>
public class POSProviderFactory : IPOSProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPOSProviderRegistry _registry;

    public POSProviderFactory(
        IServiceProvider serviceProvider,
        IPOSProviderRegistry registry)
    {
        _serviceProvider = serviceProvider;
        _registry = registry;
    }

    public IPOSProvider CreateProvider(string providerId)
    {
        if (!_registry.IsProviderRegistered(providerId))
            throw new ArgumentException($"Provider '{providerId}' is not registered", nameof(providerId));

        var providerType = _registry.GetProviderType(providerId);
        return (IPOSProvider)_serviceProvider.GetRequiredService(providerType);
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
