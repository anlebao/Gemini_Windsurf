using VanAn.Shared.Domain;
using VanAn.Unit.Tests.Domain;

namespace VanAn.Unit.Tests.Services;

/// <summary>
/// Service interface for Facebook Lead processing
/// TDD-compliant - no infrastructure dependencies
/// </summary>
public interface IFacebookLeadService
{
    Task<FacebookLead> ProcessFacebookWebhookAsync(FacebookWebhookPayload payload);
    Task<bool> ValidateFacebookWebhookAsync(string signature, string payload);
    Task<int> CalculateLeadScoreAsync(Lead lead);
}

/// <summary>
/// Service interface for Lead Management
/// </summary>
public interface ILeadManagementService
{
    Task<Lead> CreateLeadAsync(Lead lead);
    Task<Lead> UpdateLeadStatusAsync(Guid leadId, LeadStatus status, Guid? staffId = null);
    Task<Lead> AssignLeadToStaffAsync(Guid leadId, Guid staffId);
    Task<List<Lead>> GetLeadsByStatusAsync(LeadStatus status);
}

/// <summary>
/// Service interface for Lead Conversion
/// Uses production Customer from VanAn.Shared.Domain (single source of truth)
/// </summary>
public interface ILeadConversionService
{
    Task<Customer> ConvertLeadToCustomerAsync(Guid leadId, string conversionReason);
    Task<bool> ValidateLeadForConversionAsync(Guid leadId);
    Task<CustomerOnboarding> StartCustomerOnboardingAsync(Guid customerId);
}

/// <summary>
/// Service interface for Customer Onboarding
/// </summary>
public interface ICustomerOnboardingService
{
    Task<CustomerOnboarding> StartOnboardingAsync(Guid customerId);
    Task<CustomerOnboarding> UpdateOnboardingStepAsync(Guid customerId, OnboardingStep step);
    Task<CustomerOnboarding> TrackAppInstallationAsync(Guid customerId, string deviceType, string appVersion);
    Task<CustomerOnboarding> SendWelcomeMessageAsync(Guid customerId);
    Task<bool> CompleteOnboardingAsync(Guid customerId);
}

/// <summary>
/// Service interface for Loyalty Rewards
/// </summary>
public interface ILoyaltyRewardsService
{
    Task<LoyaltyRewards?> GetCustomerRewardsAsync(Guid customerId);
    Task InitializeCustomerRewardsAsync(Guid customerId, int welcomePoints);
}

/// <summary>
/// Service interface for Notifications
/// </summary>
public interface INotificationService
{
    Task SendWelcomeNotificationAsync(Guid customerId);
    Task<bool> SendWelcomeEmailAsync(string email, string fullName);
    Task SendAppInstallNotificationAsync(Guid customerId);
    Task SendOnboardingCompletionNotificationAsync(Guid customerId);
}

// Supporting classes
public class LoyaltyRewards
{
    public Guid CustomerId { get; set; }
    public int PointBalance { get; set; }
    public string Tier { get; set; } = "Bronze";
    public DateTime LastUpdated { get; set; }
}

public class FacebookWebhookPayload
{
    public string LeadId { get; set; } = string.Empty;
    public string AdId { get; set; } = string.Empty;
    public string PageId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
    public object FormData { get; set; } = new();
}
