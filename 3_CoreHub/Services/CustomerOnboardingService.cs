using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Customer Onboarding Service Implementation
    /// Manages the complete onboarding workflow for new customers
    /// </summary>
    public class CustomerOnboardingService(
        VanAnDbContext dbContext,
        ILogger<CustomerOnboardingService> logger,
        INotificationService notificationService,
        ILoyaltyRewardsService loyaltyRewardsService) : ICustomerOnboardingService
    {
        private readonly VanAnDbContext _dbContext = dbContext;
        private readonly ILogger<CustomerOnboardingService> _logger = logger;
        private readonly INotificationService _notificationService = notificationService;
        private readonly ILoyaltyRewardsService _loyaltyRewardsService = loyaltyRewardsService;

        public async Task<CustomerOnboarding> StartOnboardingAsync(Guid customerId)
        {
            _logger.LogInformation("Starting onboarding for customer {CustomerId}", customerId);

            CustomerOnboarding? existingOnboarding = await _dbContext.CustomerOnboardings
                .FirstOrDefaultAsync(o => o.CustomerId == customerId && !o.IsDeleted);

            if (existingOnboarding != null)
            {
                _logger.LogWarning("Onboarding already exists for customer {CustomerId}", customerId);
                return existingOnboarding;
            }

            CustomerOnboarding onboarding = new()
            {
                CustomerId = customerId,
                TenantId = Guid.Empty, // Will be set when customer is retrieved
                Status = OnboardingStatus.NotStarted,
                CurrentStep = OnboardingStep.Welcome,
                StartedAt = DateTime.UtcNow
            };

            _dbContext.CustomerOnboardings.Add(onboarding);
            await _dbContext.SaveChangesAsync();

            // Add initial activity
            await AddActivityAsync(customerId, OnboardingStep.Welcome, "Onboarding process started");

            _logger.LogInformation("Onboarding started for customer {CustomerId}", customerId);
            return onboarding;
        }

        public async Task<CustomerOnboarding> TrackAppInstallationAsync(Guid customerId, string deviceType, string appVersion)
        {
            _logger.LogInformation("Tracking app installation for customer {CustomerId}", customerId);

            CustomerOnboarding onboarding = await GetOrCreateOnboardingAsync(customerId);

            onboarding.HasInstalledApp = true;
            onboarding.AppInstalledAt = DateTime.UtcNow;
            onboarding.AppVersion = appVersion;
            onboarding.DeviceType = deviceType;
            onboarding.CurrentStep = OnboardingStep.AppInstall;
            onboarding.UpdatedAt = DateTime.UtcNow;

            _dbContext.CustomerOnboardings.Update(onboarding);
            await _dbContext.SaveChangesAsync();

            await AddActivityAsync(customerId, OnboardingStep.AppInstall, $"App installed: {appVersion} on {deviceType}");

            _logger.LogInformation("App installation tracked for customer {CustomerId}", customerId);
            return onboarding;
        }

        public async Task<CustomerOnboarding> CompleteOnboardingAsync(Guid customerId)
        {
            _logger.LogInformation("Completing onboarding for customer {CustomerId}", customerId);

            CustomerOnboarding onboarding = await GetOrCreateOnboardingAsync(customerId);

            onboarding.Status = OnboardingStatus.Completed;
            onboarding.CompletedAt = DateTime.UtcNow;
            onboarding.CurrentStep = OnboardingStep.Completed;
            onboarding.UpdatedAt = DateTime.UtcNow;

            _dbContext.CustomerOnboardings.Update(onboarding);
            await _dbContext.SaveChangesAsync();

            await AddActivityAsync(customerId, OnboardingStep.Completed, "Onboarding completed");

            _logger.LogInformation("Onboarding completed for customer {CustomerId}", customerId);
            return onboarding;
        }

        public async Task<bool> SendWelcomeMessageAsync(Guid customerId)
        {
            _logger.LogInformation("Sending welcome messages to customer {CustomerId}", customerId);

            CustomerOnboarding onboarding = await GetOrCreateOnboardingAsync(customerId);
            Shared.Domain.Customer? customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerId);

            if (customer == null)
            {
                _logger.LogError("Customer not found: {CustomerId}", customerId);
                return false;
            }

            bool emailSuccess = false;
            bool smsSuccess = false;

            // Send email
            if (!string.IsNullOrEmpty(customer.Email))
            {
                emailSuccess = await _notificationService.SendEmailAsync(customer.Email, "Welcome!", "Welcome to our service!");
                if (emailSuccess)
                {
                    onboarding.WelcomeEmailSent = true;
                    onboarding.WelcomeEmailSentAt = DateTime.UtcNow;
                }
            }

            // Send SMS
            smsSuccess = await _notificationService.SendSMSAsync(customer.PhoneNumber, "Welcome to our service!");
            if (smsSuccess)
            {
                onboarding.WelcomeSMSsent = true;
                onboarding.WelcomeSMSsentAt = DateTime.UtcNow;
            }

            bool overallSuccess = emailSuccess || smsSuccess;

            if (overallSuccess)
            {
                onboarding.UpdatedAt = DateTime.UtcNow;
                _dbContext.CustomerOnboardings.Update(onboarding);
                await _dbContext.SaveChangesAsync();

                await AddActivityAsync(customerId, OnboardingStep.Welcome, "Welcome messages sent");
            }

            _logger.LogInformation("Welcome messages sent to customer {CustomerId}: Email={EmailSuccess}, SMS={SMSSuccess}", customerId, emailSuccess, smsSuccess);
            return overallSuccess;
        }

        public async Task<bool> ActivateLoyaltyProgramAsync(Guid customerId, string welcomeOffer)
        {
            _logger.LogInformation("Activating loyalty program for customer {CustomerId}", customerId);

            CustomerOnboarding onboarding = await GetOrCreateOnboardingAsync(customerId);

            // Activate loyalty program through loyalty service
            bool success = await _loyaltyRewardsService.ActivateCustomerAsync(customerId);

            if (success)
            {
                onboarding.LoyaltyProgramActivated = true;
                onboarding.LoyaltyActivatedAt = DateTime.UtcNow;
                onboarding.LoyaltyWelcomeOffer = welcomeOffer;
                onboarding.CurrentStep = OnboardingStep.LoyaltyActivation;
                onboarding.UpdatedAt = DateTime.UtcNow;

                _dbContext.CustomerOnboardings.Update(onboarding);
                await _dbContext.SaveChangesAsync();

                await AddActivityAsync(customerId, OnboardingStep.LoyaltyActivation, $"Loyalty program activated with offer: {welcomeOffer}");
            }

            _logger.LogInformation("Loyalty program activation for customer {CustomerId}: {Success}", customerId, success);
            return success;
        }

        public async Task<CustomerOnboarding?> GetOnboardingAsync(Guid customerId)
        {
            return await _dbContext.CustomerOnboardings
                .FirstOrDefaultAsync(o => o.CustomerId == customerId && !o.IsDeleted);
        }

        public async Task<IEnumerable<OnboardingActivity>> GetOnboardingActivitiesAsync(Guid customerId)
        {
            return await _dbContext.OnboardingActivities
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.ActivityDate)
                .ToListAsync();
        }

        private async Task<CustomerOnboarding> GetOrCreateOnboardingAsync(Guid customerId)
        {
            CustomerOnboarding? onboarding = await GetOnboardingAsync(customerId);

            if (onboarding == null)
            {
                // Get customer to determine tenant
                Shared.Domain.Customer? customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerId) ?? throw new InvalidOperationException($"Customer {customerId} not found");
                onboarding = new CustomerOnboarding
                {
                    CustomerId = customerId,
                    TenantId = customer.TenantId,
                    Status = OnboardingStatus.NotStarted,
                    CurrentStep = OnboardingStep.Welcome
                };

                _dbContext.CustomerOnboardings.Add(onboarding);
                await _dbContext.SaveChangesAsync();
            }

            return onboarding;
        }

        private async Task AddActivityAsync(Guid customerId, OnboardingStep step, string description)
        {
            OnboardingActivity activity = new()
            {
                CustomerId = customerId,
                Step = step,
                Description = description,
                IsCompleted = true,
                CompletedAt = DateTime.UtcNow
            };

            _dbContext.OnboardingActivities.Add(activity);
            await _dbContext.SaveChangesAsync();
        }
    }
}
