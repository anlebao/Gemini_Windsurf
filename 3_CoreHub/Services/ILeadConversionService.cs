using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Service interface for Lead to Customer Conversion
    /// Handles the conversion of qualified leads to customers
    /// </summary>
    public interface ILeadConversionService
    {
        /// <summary>
        /// Converts a lead to a customer
        /// Creates a customer record, updates lead status, and initiates onboarding
        /// </summary>
        /// <param name="leadId">The ID of the lead to convert</param>
        /// <param name="conversionReason">The reason for conversion</param>
        /// <returns>The created customer entity</returns>
        Task<Customer> ConvertLeadToCustomerAsync(Guid leadId, string conversionReason);
    }
}
