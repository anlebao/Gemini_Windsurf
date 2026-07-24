using VanAn.CoreHub.Domain.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Phase 5: Customer segmentation service contract for bulk push campaigns.
    /// </summary>
    public interface ICustomerSegmentationService
    {
        Task<IReadOnlyList<Customer>> GetCustomersBySegmentAsync(CustomerSegmentCriteria criteria);
    }
}
