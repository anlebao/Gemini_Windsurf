using System.Reflection;
using VanAn.CoreHub.Services.Providers.POS;

namespace VanAn.CoreHub.Services.Providers.EInvoice;

/// <summary>
/// EInvoiceProviderRegistry - Registry implementation for E-Invoice providers
/// Auto-discovery pattern with reflection
/// </summary>
public class EInvoiceProviderRegistry : IEInvoiceProviderRegistry
{
    private readonly Dictionary<string, Type> _providers = new();

    public void RegisterProvider(string providerId, Type providerType)
    {
        if (!typeof(IEInvoiceProvider).IsAssignableFrom(providerType))
            throw new ArgumentException($"Type {providerType.Name} does not implement IEInvoiceProvider", nameof(providerType));

        _providers[providerId] = providerType;
    }

    public bool IsProviderRegistered(string providerId)
    {
        return _providers.ContainsKey(providerId);
    }

    public Type GetProviderType(string providerId)
    {
        if (!_providers.TryGetValue(providerId, out var providerType))
            throw new ArgumentException($"Provider '{providerId}' is not registered", nameof(providerId));

        return providerType;
    }

    public IEnumerable<string> GetRegisteredProviders()
    {
        return _providers.Keys;
    }

    /// <summary>
    /// Auto-discover and register providers from assembly
    /// </summary>
    public void AutoRegisterFromAssembly(Assembly assembly)
    {
        var providerTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IEInvoiceProvider).IsAssignableFrom(t))
            .Where(t => t.GetCustomAttribute<ProviderAttribute>() != null);

        foreach (var providerType in providerTypes)
        {
            var attribute = providerType.GetCustomAttribute<ProviderAttribute>();
            if (attribute != null)
            {
                RegisterProvider(attribute.ProviderId, providerType);
            }
        }
    }
}
