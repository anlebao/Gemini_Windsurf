using VanAn.Shared.Domain;
using VanAn.Unit.Tests.Domain;

namespace VanAn.Unit.Tests.Repositories;

/// <summary>
/// Repository interface for Lead entities
/// TDD-compliant - no infrastructure dependencies
/// </summary>
public interface ILeadRepository
{
    Task AddAsync(Lead lead);
    Task<Lead?> GetByIdAsync(Guid id);
    Task<List<Lead>> GetByStatusAsync(LeadStatus status);
    Task UpdateAsync(Lead lead);
    Task<List<Lead>> GetAllAsync();
}

/// <summary>
/// Repository interface for FacebookLead entities
/// </summary>
public interface IFacebookLeadRepository
{
    Task AddAsync(FacebookLead facebookLead);
    Task<FacebookLead?> GetByFacebookLeadIdAsync(string facebookLeadId);
    Task<List<FacebookLead>> GetUnprocessedAsync();
    Task UpdateAsync(FacebookLead facebookLead);
}

/// <summary>
/// Repository interface for LeadActivity entities
/// </summary>
public interface ILeadActivityRepository
{
    Task AddAsync(LeadActivity activity);
    Task<List<LeadActivity>> GetByLeadIdAsync(Guid leadId);
    Task<List<LeadActivity>> GetByTypeAsync(LeadActivityType activityType);
}

/// <summary>
/// Repository interface for Customer entities
/// Uses production Customer from VanAn.Shared.Domain (single source of truth)
/// </summary>
public interface ICustomerRepository
{
    Task AddAsync(Customer customer);
    Task<Customer?> GetByIdAsync(Guid id);
    Task<Customer?> GetByPhoneNumberAsync(string phoneNumber);
    Task UpdateAsync(Customer customer);
    Task<List<Customer>> GetAllAsync();
}

/// <summary>
/// Repository interface for CustomerOnboarding entities
/// </summary>
public interface ICustomerOnboardingRepository
{
    Task AddAsync(CustomerOnboarding onboarding);
    Task<CustomerOnboarding?> GetByCustomerIdAsync(Guid customerId);
    Task UpdateAsync(CustomerOnboarding onboarding);
    Task<List<CustomerOnboarding>> GetByStatusAsync(OnboardingStatus status);
}

/// <summary>
/// Repository interface for OnboardingActivity entities
/// </summary>
public interface IOnboardingActivityRepository
{
    Task AddAsync(OnboardingActivity activity);
    Task<List<OnboardingActivity>> GetByCustomerIdAsync(Guid customerId);
    Task<List<OnboardingActivity>> GetByStepAsync(OnboardingStep step);
}
