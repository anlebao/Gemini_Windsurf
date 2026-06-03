using VanAn.CoreHub.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Customer Onboarding Service Interface
    /// Manages the onboarding process for new customers
    /// </summary>
    public interface ICustomerOnboardingService
    {
        /// <summary>
        /// Starts the onboarding process for a customer
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <returns>Customer onboarding record</returns>
        Task<CustomerOnboarding> StartOnboardingAsync(Guid customerId);

        /// <summary>
        /// Tracks app installation during onboarding
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <param name="deviceType">Device type</param>
        /// <param name="appVersion">App version</param>
        /// <returns>Updated onboarding record</returns>
        Task<CustomerOnboarding> TrackAppInstallationAsync(Guid customerId, string deviceType, string appVersion);

        /// <summary>
        /// Completes the onboarding process
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <returns>Completed onboarding record</returns>
        Task<CustomerOnboarding> CompleteOnboardingAsync(Guid customerId);

        /// <summary>
        /// Sends welcome message (email/SMS) to customer
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <returns>Success status</returns>
        Task<bool> SendWelcomeMessageAsync(Guid customerId);

        /// <summary>
        /// Activates loyalty program for customer
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <param name="welcomeOffer">Welcome offer details</param>
        /// <returns>Success status</returns>
        Task<bool> ActivateLoyaltyProgramAsync(Guid customerId, string welcomeOffer);

        /// <summary>
        /// Gets onboarding status for a customer
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <returns>Customer onboarding record</returns>
        Task<CustomerOnboarding?> GetOnboardingAsync(Guid customerId);

        /// <summary>
        /// Gets all onboarding activities for a customer
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <returns>List of onboarding activities</returns>
        Task<IEnumerable<OnboardingActivity>> GetOnboardingActivitiesAsync(Guid customerId);
    }
}
