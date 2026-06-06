using System.Reflection;

namespace VanAn.CoreHub.Services.Providers.POS;

/// <summary>
/// ProviderAttribute - Attribute for auto-registration of providers
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ProviderAttribute : Attribute
{
    public string ProviderId { get; }

    public ProviderAttribute(string providerId)
    {
        ProviderId = providerId;
    }
}

/// <summary>
/// POSProviderRegistry - Registry implementation for POS providers
/// Auto-discovery pattern with reflection
/// </summary>
public class POSProviderRegistry : IPOSProviderRegistry
{
    private readonly Dictionary<string, Type> _providers = new();

    public void RegisterProvider(string providerId, Type providerType)
    {
        if (!typeof(IPOSProvider).IsAssignableFrom(providerType))
            throw new ArgumentException($"Type {providerType.Name} does not implement IPOSProvider", nameof(providerType));

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
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IPOSProvider).IsAssignableFrom(t))
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
