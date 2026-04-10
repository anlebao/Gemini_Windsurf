using VanAn.Unit.Tests.Domain;

namespace VanAn.Unit.Tests.Repositories;

/// <summary>
/// In-memory implementation of ILeadRepository
/// TDD-compliant - no database dependencies
/// </summary>
public class InMemoryLeadRepository : ILeadRepository
{
    private readonly List<Lead> _leads = new();

    public Task AddAsync(Lead lead)
    {
        _leads.Add(lead);
        return Task.CompletedTask;
    }

    public Task<Lead?> GetByIdAsync(Guid id)
    {
        return Task.FromResult(_leads.FirstOrDefault(x => x.Id == id));
    }

    public Task<List<Lead>> GetByStatusAsync(LeadStatus status)
    {
        return Task.FromResult(_leads.Where(x => x.Status == status).ToList());
    }

    public Task UpdateAsync(Lead lead)
    {
        var existing = _leads.FirstOrDefault(x => x.Id == lead.Id);
        if (existing != null)
        {
            var index = _leads.IndexOf(existing);
            _leads[index] = lead;
        }
        return Task.CompletedTask;
    }

    public Task<List<Lead>> GetAllAsync()
    {
        return Task.FromResult(_leads.ToList());
    }
}

/// <summary>
/// In-memory implementation of IFacebookLeadRepository
/// </summary>
public class InMemoryFacebookLeadRepository : IFacebookLeadRepository
{
    private readonly List<FacebookLead> _facebookLeads = new();

    public Task AddAsync(FacebookLead facebookLead)
    {
        _facebookLeads.Add(facebookLead);
        return Task.CompletedTask;
    }

    public Task<FacebookLead?> GetByFacebookLeadIdAsync(string facebookLeadId)
    {
        return Task.FromResult(_facebookLeads.FirstOrDefault(x => x.FacebookLeadId == facebookLeadId));
    }

    public Task<List<FacebookLead>> GetUnprocessedAsync()
    {
        return Task.FromResult(_facebookLeads.Where(x => !x.IsFacebookProcessed).ToList());
    }

    public Task UpdateAsync(FacebookLead facebookLead)
    {
        var existing = _facebookLeads.FirstOrDefault(x => x.Id == facebookLead.Id);
        if (existing != null)
        {
            var index = _facebookLeads.IndexOf(existing);
            _facebookLeads[index] = facebookLead;
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory implementation of ILeadActivityRepository
/// </summary>
public class InMemoryLeadActivityRepository : ILeadActivityRepository
{
    private readonly List<LeadActivity> _activities = new();

    public Task AddAsync(LeadActivity activity)
    {
        _activities.Add(activity);
        return Task.CompletedTask;
    }

    public Task<List<LeadActivity>> GetByLeadIdAsync(Guid leadId)
    {
        return Task.FromResult(_activities.Where(x => x.LeadId == leadId).ToList());
    }

    public Task<List<LeadActivity>> GetByTypeAsync(LeadActivityType activityType)
    {
        return Task.FromResult(_activities.Where(x => x.ActivityType == activityType).ToList());
    }
}

/// <summary>
/// In-memory implementation of ICustomerRepository
/// </summary>
public class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly List<Customer> _customers = new();

    public Task AddAsync(Customer customer)
    {
        _customers.Add(customer);
        return Task.CompletedTask;
    }

    public Task<Customer?> GetByIdAsync(Guid id)
    {
        return Task.FromResult(_customers.FirstOrDefault(x => x.Id == id));
    }

    public Task<Customer?> GetByPhoneNumberAsync(string phoneNumber)
    {
        return Task.FromResult(_customers.FirstOrDefault(x => x.PhoneNumber == phoneNumber));
    }

    public Task UpdateAsync(Customer customer)
    {
        var existing = _customers.FirstOrDefault(x => x.Id == customer.Id);
        if (existing != null)
        {
            var index = _customers.IndexOf(existing);
            _customers[index] = customer;
        }
        return Task.CompletedTask;
    }

    public Task<List<Customer>> GetAllAsync()
    {
        return Task.FromResult(_customers.ToList());
    }
}

/// <summary>
/// In-memory implementation of ICustomerOnboardingRepository
/// </summary>
public class InMemoryCustomerOnboardingRepository : ICustomerOnboardingRepository
{
    private readonly List<CustomerOnboarding> _onboardings = new();

    public Task AddAsync(CustomerOnboarding onboarding)
    {
        _onboardings.Add(onboarding);
        return Task.CompletedTask;
    }

    public Task<CustomerOnboarding?> GetByCustomerIdAsync(Guid customerId)
    {
        return Task.FromResult(_onboardings.FirstOrDefault(x => x.CustomerId == customerId));
    }

    public Task UpdateAsync(CustomerOnboarding onboarding)
    {
        var existing = _onboardings.FirstOrDefault(x => x.Id == onboarding.Id);
        if (existing != null)
        {
            var index = _onboardings.IndexOf(existing);
            _onboardings[index] = onboarding;
        }
        return Task.CompletedTask;
    }

    public Task<List<CustomerOnboarding>> GetByStatusAsync(OnboardingStatus status)
    {
        return Task.FromResult(_onboardings.Where(x => x.Status == status).ToList());
    }
}

/// <summary>
/// In-memory implementation of IOnboardingActivityRepository
/// </summary>
public class InMemoryOnboardingActivityRepository : IOnboardingActivityRepository
{
    private readonly List<OnboardingActivity> _activities = new();

    public Task AddAsync(OnboardingActivity activity)
    {
        _activities.Add(activity);
        return Task.CompletedTask;
    }

    public Task<List<OnboardingActivity>> GetByCustomerIdAsync(Guid customerId)
    {
        return Task.FromResult(_activities.Where(x => x.CustomerId == customerId).ToList());
    }

    public Task<List<OnboardingActivity>> GetByStepAsync(OnboardingStep step)
    {
        return Task.FromResult(_activities.Where(x => x.Step == step).ToList());
    }
}
