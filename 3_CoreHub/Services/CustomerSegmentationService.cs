using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Phase 5: Customer segmentation service for bulk push campaigns.
    /// Wraps ICustomerRepository.GetBySegmentAsync with criteria validation + logging.
    /// </summary>
    public class CustomerSegmentationService(ICustomerRepository customerRepository, ILogger<CustomerSegmentationService> logger) : ICustomerSegmentationService
    {
        private readonly ICustomerRepository _customerRepository = customerRepository;
        private readonly ILogger<CustomerSegmentationService> _logger = logger;

        public async Task<IReadOnlyList<Customer>> GetCustomersBySegmentAsync(CustomerSegmentCriteria criteria)
        {
            _logger.LogInformation("Segmenting customers: Tier={Tier}, MinIdentity={MinIdentity}, MinSpent={MinSpent}, MaxSpent={MaxSpent}, LastOrderAfter={LastOrderAfter}, HasPush={HasPush}",
                criteria.CustomerTier ?? "any",
                criteria.MinIdentityLevel?.ToString() ?? "any",
                criteria.MinTotalSpent?.ToString() ?? "any",
                criteria.MaxTotalSpent?.ToString() ?? "any",
                criteria.LastOrderAfter?.ToString("yyyy-MM-dd") ?? "any",
                criteria.HasPushSubscription);

            IReadOnlyList<Customer> customers = await _customerRepository.GetBySegmentAsync(criteria);

            _logger.LogInformation("Segmentation returned {Count} customers", customers.Count);
            return customers;
        }
    }
}
