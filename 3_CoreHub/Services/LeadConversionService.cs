using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Lead to Customer Conversion Service Implementation
    /// Handles the conversion of qualified leads to customers
    /// </summary>
    public class LeadConversionService(
        VanAnDbContext dbContext,
        ILogger<LeadConversionService> logger,
        ILeadManagementService leadManagementService,
        ILoyaltyRewardsService loyaltyRewardsService,
        ICustomerOnboardingService customerOnboardingService) : ILeadConversionService
    {
        private readonly VanAnDbContext _dbContext = dbContext;
        private readonly ILogger<LeadConversionService> _logger = logger;
        private readonly ILeadManagementService _leadManagementService = leadManagementService;
        private readonly ILoyaltyRewardsService _loyaltyRewardsService = loyaltyRewardsService;
        private readonly ICustomerOnboardingService _customerOnboardingService = customerOnboardingService;

        public async Task<Customer> ConvertLeadToCustomerAsync(Guid leadId, string conversionReason)
        {
            _logger.LogInformation("Converting lead {LeadId} to customer", leadId);

            // Start transaction for atomic conversion
            using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // Get the lead
                Lead? lead = await _dbContext.Leads.FindAsync(leadId);
                if (lead == null)
                {
                    _logger.LogWarning("Lead {LeadId} not found", leadId);
                    throw new ArgumentException($"Lead with ID {leadId} not found");
                }

                // Validate lead status
                if (lead.Status != LeadStatus.Qualified)
                {
                    _logger.LogWarning("Lead {LeadId} is not qualified for conversion (Status: {Status})", leadId, lead.Status);
                    throw new InvalidOperationException($"Lead is unqualified for conversion. Current status: {lead.Status}");
                }

                // Check for duplicate customer with same phone number
                // PhoneNumber is PII-encrypted + TenantId is value object — both require client-side evaluation
                List<Customer> tenantCustomers = await _dbContext.Customers.ToListAsync();
                bool duplicateExists = tenantCustomers.Any(c =>
                    c.PhoneNumber == lead.PhoneNumber &&
                    c.TenantId.Value == lead.TenantId);
                if (duplicateExists)
                {
                    throw new InvalidOperationException($"A customer with phone number already exists in this tenant");
                }

                // Create customer using domain constructor
                Customer customer = new Customer(
                    new TenantId(lead.TenantId),
                    lead.FullName,
                    lead.PhoneNumber,
                    lead.Email
                );

                _ = _dbContext.Customers.Add(customer);
                _ = await _dbContext.SaveChangesAsync();

                // Update lead status
                lead.Status = LeadStatus.Converted;
                lead.ConvertedCustomerId = customer.Id;
                lead.ConversionDate = DateTime.UtcNow;
                lead.ConversionReason = conversionReason;
                lead.UpdatedAt = DateTime.UtcNow;

                _ = await _leadManagementService.UpdateLeadStatusAsync(leadId, LeadStatus.Converted);

                // Initialize loyalty rewards for new customer
                _logger.LogInformation("Initializing loyalty rewards for customer {CustomerId}", customer.Id);
                _ = await _loyaltyRewardsService.GetOrCreateCustomerRewardsAsync(customer.Id, new TenantId(lead.TenantId));

                // Start customer onboarding
                _logger.LogInformation("Starting onboarding for customer {CustomerId}", customer.Id);
                _ = await _customerOnboardingService.StartOnboardingAsync(customer.Id);

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
}
