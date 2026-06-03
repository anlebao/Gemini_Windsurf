using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Implementation of customer management service
    /// Engineering Constitution: Uses repository pattern with tenant enforcement and soft delete compliance
    /// </summary>
    public class CustomerService(ICustomerRepository repository, ILogger<CustomerService> logger) : ICustomerService
    {
        private readonly ICustomerRepository _repository = repository;
        private readonly ILogger<CustomerService> _logger = logger;

        public async Task<Customer?> GetCustomerByDeviceIdAsync(Guid deviceId)
        {
            try
            {
                _logger.LogDebug("Looking up customer by device ID: {DeviceId}", deviceId);

                Customer? customer = await _repository.GetByDeviceIdAsync(deviceId);

                if (customer != null)
                {
                    _logger.LogDebug("Found customer {CustomerId} for device ID: {DeviceId}",
                        customer.CustomerId, deviceId);
                }
                else
                {
                    _logger.LogDebug("No customer found for device ID: {DeviceId}", deviceId);
                }

                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error looking up customer by device ID: {DeviceId}", deviceId);
                throw;
            }
        }

        public async Task<Customer> GetOrCreateCustomerByDeviceIdAsync(Guid deviceId, string? displayName = null)
        {
            try
            {
                _logger.LogDebug("Getting or creating customer for device ID: {DeviceId}", deviceId);

                // First, try to find existing customer
                Customer? existingCustomer = await GetCustomerByDeviceIdAsync(deviceId);

                if (existingCustomer != null)
                {
                    _logger.LogDebug("Returning existing customer {CustomerId} for device ID: {DeviceId}",
                        existingCustomer.CustomerId, deviceId);
                    return existingCustomer;
                }

                // Create new customer if not found
                TenantId tenantId = new(Guid.NewGuid()); // Will be set by repository
                Customer newCustomer = new(tenantId, displayName ?? "Khách hàng anonymity", "Unknown");
                newCustomer.UpdateCustomerDetails(displayName ?? "Khách hàng anonymity", "Unknown", null, "Bronze", deviceId, true);

                Customer createdCustomer = await _repository.AddAsync(newCustomer);

                _logger.LogInformation("Created new customer {CustomerId} for device ID: {DeviceId}",
                    createdCustomer.CustomerId, deviceId);

                return createdCustomer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting or creating customer for device ID: {DeviceId}", deviceId);
                throw;
            }
        }
    }
}
