using VanAn.Shared.Domain;

namespace VanAn.Shared.Services;

/// <summary>
/// Customer management service interface
/// Provides customer lookup and creation functionality for omnichannel support
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// Get customer by device ID
    /// </summary>
    /// <param name="deviceId">The device ID to search for</param>
    /// <returns>Customer if found, null otherwise</returns>
    Task<Customer?> GetCustomerByDeviceIdAsync(Guid deviceId);

    /// <summary>
    /// Get existing customer by device ID or create new one if not found
    /// </summary>
    /// <param name="deviceId">The device ID to search for or associate with new customer</param>
    /// <param name="displayName">Optional display name for new customer</param>
    /// <returns>Existing or newly created customer</returns>
    Task<Customer> GetOrCreateCustomerByDeviceIdAsync(Guid deviceId, string? displayName = null);
}
