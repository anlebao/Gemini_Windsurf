namespace VanAn.CoreHub.Services.Providers.POS;

/// <summary>
/// IPOSProviderFactory - Factory for creating POS provider instances
/// Stateless factory pattern for provider instantiation
/// </summary>
public interface IPOSProviderFactory
{
    /// <summary>
    /// Create provider instance by provider ID
    /// </summary>
    IPOSProvider CreateProvider(string providerId);

    /// <summary>
    /// Check if provider is registered
    /// </summary>
    bool IsProviderRegistered(string providerId);

    /// <summary>
    /// Get all registered provider IDs
    /// </summary>
    IEnumerable<string> GetRegisteredProviders();
}
