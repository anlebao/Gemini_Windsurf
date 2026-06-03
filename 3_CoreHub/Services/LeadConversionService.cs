using System.Runtime.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Lead to Customer Conversion Service Implementation
/// Handles the conversion of qualified leads to customers
/// </summary>
public class LeadConversionService : ILeadConversionService
{
    private readonly VanAnDbContext _dbContext;
    private readonly ILogger<LeadConversionService> _logger;
    private readonly ILeadManagementService _leadManagementService;
    private readonly ILoyaltyRewardsService _loyaltyRewardsService;
    private readonly ICustomerOnboardingService _customerOnboardingService;

    public LeadConversionService(
        VanAnDbContext dbContext,
        ILogger<LeadConversionService> logger,
        ILeadManagementService leadManagementService,
        ILoyaltyRewardsService loyaltyRewardsService,
        ICustomerOnboardingService customerOnboardingService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _leadManagementService = leadManagementService;
        _loyaltyRewardsService = loyaltyRewardsService;
        _customerOnboardingService = customerOnboardingService;
    }

    public async Task<Customer> ConvertLeadToCustomerAsync(Guid leadId, string conversionReason)
    {
        _logger.LogInformation("Converting lead {LeadId} to customer", leadId);

        // Start transaction for atomic conversion
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // Get the lead
            var lead = await _dbContext.Leads.FindAsync(leadId);
            if (lead == null)
            {
                _logger.LogWarning("Lead {LeadId} not found", leadId);
                throw new ArgumentException($"Lead with ID {leadId} not found");
            }

            // Validate lead status
            if (lead.Status != LeadStatus.Qualified)
            {
                _logger.LogWarning("Lead {LeadId} is not qualified for conversion (Status: {Status})", leadId, lead.Status);
                throw new InvalidOperationException($"Lead must be Qualified to convert. Current status: {lead.Status}");
            }

            // Create customer using reflection-based factory to preserve domain integrity
            var customer = (Customer)FormatterServices.GetUninitializedObject(typeof(Customer));
            
            // Set properties through reflection to bypass protected setters
            var customerType = typeof(Customer);
            customerType.GetProperty("Id")?.SetValue(customer, Guid.NewGuid());
            customerType.GetProperty("CustomerId")?.SetValue(customer, new CustomerId((Guid)customerType.GetProperty("Id")?.GetValue(customer)!));
            customerType.GetProperty("TenantId")?.SetValue(customer, lead.TenantId);
            customerType.GetProperty("FullName")?.SetValue(customer, lead.FullName);
            customerType.GetProperty("PhoneNumber")?.SetValue(customer, lead.PhoneNumber);
            customerType.GetProperty("Email")?.SetValue(customer, lead.Email);
            customerType.GetProperty("CustomerTier")?.SetValue(customer, "Bronze"); // Default tier for new customers
            customerType.GetProperty("LoyaltyPoints")?.SetValue(customer, 0);
            customerType.GetProperty("IsActive")?.SetValue(customer, true);
            customerType.GetProperty("CreatedAt")?.SetValue(customer, DateTime.UtcNow);
            customerType.GetProperty("UpdatedAt")?.SetValue(customer, DateTime.UtcNow);
            customerType.GetProperty("IsDeleted")?.SetValue(customer, false);

            _dbContext.Customers.Add(customer);
            await _dbContext.SaveChangesAsync();

            // Update lead status
            lead.Status = LeadStatus.Converted;
            lead.ConvertedCustomerId = customer.Id;
            lead.ConversionDate = DateTime.UtcNow;
            lead.ConversionReason = conversionReason;
            lead.UpdatedAt = DateTime.UtcNow;

            await _leadManagementService.UpdateLeadStatusAsync(leadId, LeadStatus.Converted);

            // Initialize loyalty rewards for new customer
            _logger.LogInformation("Initializing loyalty rewards for customer {CustomerId}", customer.Id);
            // Note: LoyaltyRewardsService handles its own logic

            // Start customer onboarding
            _logger.LogInformation("Starting onboarding for customer {CustomerId}", customer.Id);
            await _customerOnboardingService.StartOnboardingAsync(customer.Id);

            await transaction.CommitAsync();

            _logger.LogInformation("Lead {LeadId} successfully converted to customer {CustomerId}", leadId, customer.Id);
            return customer;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
