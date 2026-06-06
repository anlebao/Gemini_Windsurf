namespace VanAn.CoreHub.Services.Providers.EInvoice;

/// <summary>
/// IEInvoiceProviderFactory - Factory for creating E-Invoice provider instances
/// Stateless factory pattern for provider instantiation
/// </summary>
public interface IEInvoiceProviderFactory
{
    /// <summary>
    /// Create provider instance by provider ID
    /// </summary>
    IEInvoiceProvider CreateProvider(string providerId);

    /// <summary>
    /// Check if provider is registered
    /// </summary>
    bool IsProviderRegistered(string providerId);

    /// <summary>
    /// Get all registered provider IDs
    /// </summary>
    IEnumerable<string> GetRegisteredProviders();
}
