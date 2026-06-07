namespace VanAn.CoreHub.Services.Providers.POS;

/// <summary>
/// IPOSProviderRegistry - Registry for POS provider types
/// Auto-discovery pattern with ProviderAttribute
/// </summary>
public interface IPOSProviderRegistry
{
    /// <summary>
    /// Register provider type
    /// </summary>
    void RegisterProvider(string providerId, Type providerType);

    /// <summary>
    /// Check if provider is registered
    /// </summary>
    bool IsProviderRegistered(string providerId);

    /// <summary>
    /// Get provider type by provider ID
    /// </summary>
    Type GetProviderType(string providerId);

    /// <summary>
    /// Get all registered provider IDs
    /// </summary>
    IEnumerable<string> GetRegisteredProviders();
}
