using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Json;
using Xunit;
using VanAn.Shared.Domain;
using VanAn.API.Tests.Infrastructure;

namespace VanAn.API.Tests;

/// <summary>
/// API tests for Customer Onboarding
/// Layer 3: API Tests - Customer Onboarding API
/// </summary>
public class CustomerOnboardingAPITests : ApiTestBase
{
    private readonly HttpClient _client;
    private readonly VanAnDbContext _dbContext;

    public CustomerOnboardingAPITests()
    {
        _client = Factory.CreateClient();
        _dbContext = Factory.Services.GetRequiredService<VanAnDbContext>();
    }

    [Fact(DisplayName = "POST_Onboarding_Start_ShouldCreateRecord")]
    public async Task POST_Onboarding_Start_ShouldCreateRecord()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Onboarding Test Customer",
            PhoneNumber = "0987654321",
            Email = "onboarding@test.com",
            CustomerTier = "Bronze",
            LoyaltyPoints = 50,
            IsActive = true,
            TenantId = tenantId
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        SetTenantContext(tenantId);

        var startRequest = new
        {
            customer_id = customer.Id,
            welcome_message = "Welcome to Vàn An Coffee!"
        };

        // Act
        var response = await _client.PostAsync($"/api/customers/{customer.Id}/onboarding/start", 
            CreateJsonContent(startRequest));

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<CustomerOnboardingResponse>();
        Assert.NotNull(responseContent);
        Assert.Equal(customer.Id, responseContent.CustomerId);
        Assert.Equal("InProgress", responseContent.Status);
        Assert.Equal("Welcome", responseContent.CurrentStep);
        Assert.True(responseContent.StartedAt.HasValue);
        Assert.False(responseContent.CompletedAt.HasValue);

        // Verify in database
        var onboarding = await _dbContext.CustomerOnboardings
            .FirstOrDefaultAsync(o => o.CustomerId == customer.Id);

        Assert.NotNull(onboarding);
        Assert.Equal(OnboardingStatus.InProgress, onboarding.Status);
        Assert.Equal(OnboardingStep.Welcome, onboarding.CurrentStep);
        Assert.NotNull(onboarding.StartedAt);
    }

    [Fact(DisplayName = "PUT_Onboarding_AppInstall_ShouldTrackInstallation")]
    public async Task PUT_Onboarding_AppInstall_ShouldTrackInstallation()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "App Install Customer",
            PhoneNumber = "0912345678",
            Email = "appinstall@test.com",
            CustomerTier = "Bronze",
            LoyaltyPoints = 50,
            IsActive = true,
            TenantId = tenantId
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        // Start onboarding first
        var onboarding = new CustomerOnboarding
        {
            CustomerId = customer.Id,
            Status = OnboardingStatus.InProgress,
            CurrentStep = OnboardingStep.ProfileSetup,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            TenantId = tenantId
        };

        _dbContext.CustomerOnboardings.Add(onboarding);
        await _dbContext.SaveChangesAsync();

        SetTenantContext(tenantId);

        var appInstallRequest = new
        {
            device_type = "iOS",
            app_version = "1.0.0",
            installation_id = Guid.NewGuid().ToString()
        };

        // Act
        var response = await _client.PutAsync($"/api/customers/{customer.Id}/onboarding/app-install", 
            CreateJsonContent(appInstallRequest));

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<CustomerOnboardingResponse>();
        Assert.NotNull(responseContent);
        Assert.True(responseContent.HasInstalledApp);
        Assert.Equal("iOS", responseContent.DeviceType);
        Assert.Equal("1.0.0", responseContent.AppVersion);
        Assert.True(responseContent.AppInstalledAt.HasValue);
        Assert.Equal("AppInstall", responseContent.CurrentStep);

        // Verify in database
        var updatedOnboarding = await _dbContext.CustomerOnboardings
            .FirstOrDefaultAsync(o => o.CustomerId == customer.Id);

        Assert.NotNull(updatedOnboarding);
        Assert.True(updatedOnboarding.HasInstalledApp);
        Assert.Equal("iOS", updatedOnboarding.DeviceType);
        Assert.Equal("1.0.0", updatedOnboarding.AppVersion);
        Assert.True(updatedOnboarding.AppInstalledAt.HasValue);
    }

    [Fact(DisplayName = "GET_Onboarding_Status_ShouldReturnProgress")]
    public async Task GET_Onboarding_Status_ShouldReturnProgress()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Status Check Customer",
            PhoneNumber = "0923456789",
            Email = "status@test.com",
            CustomerTier = "Bronze",
            LoyaltyPoints = 50,
            IsActive = true,
            TenantId = tenantId
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        // Create onboarding with progress
        var onboarding = new CustomerOnboarding
        {
            CustomerId = customer.Id,
            Status = OnboardingStatus.InProgress,
            CurrentStep = OnboardingStep.LoyaltyActivation,
            StartedAt = DateTime.UtcNow.AddDays(-1),
            HasInstalledApp = true,
            AppInstalledAt = DateTime.UtcNow.AddHours(-12),
            WelcomeEmailSent = true,
            WelcomeEmailSentAt = DateTime.UtcNow.AddHours(-18),
            TenantId = tenantId
        };

        _dbContext.CustomerOnboardings.Add(onboarding);
        await _dbContext.SaveChangesAsync();

        SetTenantContext(tenantId);

        // Act
        var response = await _client.GetAsync($"/api/customers/{customer.Id}/onboarding/status");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<CustomerOnboardingStatusResponse>();
        Assert.NotNull(responseContent);
        Assert.Equal(customer.Id, responseContent.CustomerId);
        Assert.Equal("InProgress", responseContent.Status);
        Assert.Equal("LoyaltyActivation", responseContent.CurrentStep);
        Assert.True(responseContent.HasInstalledApp);
        Assert.True(responseContent.WelcomeEmailSent);
        Assert.False(responseContent.LoyaltyProgramActivated);

        // Verify progress percentage
        Assert.True(responseContent.ProgressPercentage > 50); // Should be more than halfway
    }

    [Fact(DisplayName = "POST_Onboarding_Complete_ShouldActivateCustomer")]
    public async Task POST_Onboarding_Complete_ShouldActivateCustomer()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Complete Onboarding Customer",
            PhoneNumber = "0934567890",
            Email = "complete@test.com",
            CustomerTier = "Bronze",
            LoyaltyPoints = 50,
            IsActive = false, // Not yet activated
            TenantId = tenantId
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        // Create onboarding with all required steps completed
        var onboarding = new CustomerOnboarding
        {
            CustomerId = customer.Id,
            Status = OnboardingStatus.InProgress,
            CurrentStep = OnboardingStep.LoyaltyActivation,
            StartedAt = DateTime.UtcNow.AddDays(-1),
            HasInstalledApp = true,
            AppInstalledAt = DateTime.UtcNow.AddHours(-12),
            WelcomeEmailSent = true,
            WelcomeEmailSentAt = DateTime.UtcNow.AddHours(-18),
            TenantId = tenantId
        };

        _dbContext.CustomerOnboardings.Add(onboarding);
        await _dbContext.SaveChangesAsync();

        SetTenantContext(tenantId);

        var completeRequest = new
        {
            completion_notes = "Customer completed all onboarding steps successfully",
            loyalty_program_activated = true
        };

        // Act
        var response = await _client.PostAsync($"/api/customers/{customer.Id}/onboarding/complete", 
            CreateJsonContent(completeRequest));

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<CustomerOnboardingResponse>();
        Assert.NotNull(responseContent);
        Assert.Equal("Completed", responseContent.Status);
        Assert.Equal("Completed", responseContent.CurrentStep);
        Assert.True(responseContent.CompletedAt.HasValue);

        // Verify customer activated
        var activatedCustomer = await _dbContext.Customers.FindAsync(customer.Id);
        Assert.NotNull(activatedCustomer);
        Assert.True(activatedCustomer.IsActive);

        // Verify onboarding completed
        var completedOnboarding = await _dbContext.CustomerOnboardings
            .FirstOrDefaultAsync(o => o.CustomerId == customer.Id);

        Assert.NotNull(completedOnboarding);
        Assert.Equal(OnboardingStatus.Completed, completedOnboarding.Status);
        Assert.Equal(OnboardingStep.Completed, completedOnboarding.CurrentStep);
        Assert.True(completedOnboarding.LoyaltyProgramActivated);
        Assert.True(completedOnboarding.CompletedAt.HasValue);
    }

    [Fact(DisplayName = "POST_Onboarding_WelcomeEmail_ShouldSendEmail")]
    public async Task POST_Onboarding_WelcomeEmail_ShouldSendEmail()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Welcome Email Customer",
            PhoneNumber = "0945678901",
            Email = "welcomeemail@test.com",
            CustomerTier = "Bronze",
            LoyaltyPoints = 50,
            IsActive = true,
            TenantId = tenantId
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        // Start onboarding
        var onboarding = new CustomerOnboarding
        {
            CustomerId = customer.Id,
            Status = OnboardingStatus.InProgress,
            CurrentStep = OnboardingStep.Welcome,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            TenantId = tenantId
        };

        _dbContext.CustomerOnboardings.Add(onboarding);
        await _dbContext.SaveChangesAsync();

        SetTenantContext(tenantId);

        var emailRequest = new
        {
            template_type = "welcome",
            personalization = new
            {
                customer_name = customer.FullName,
                welcome_offer = "20% discount on first order",
                loyalty_points = 50
            }
        };

        // Act
        var response = await _client.PostAsync($"/api/customers/{customer.Id}/onboarding/welcome-email", 
            CreateJsonContent(emailRequest));

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<EmailResponse>();
        Assert.NotNull(responseContent);
        Assert.True(responseContent.Sent);
        Assert.Equal("welcomeemail@test.com", responseContent.RecipientEmail);

        // Verify onboarding updated
        var updatedOnboarding = await _dbContext.CustomerOnboardings
            .FirstOrDefaultAsync(o => o.CustomerId == customer.Id);

        Assert.NotNull(updatedOnboarding);
        Assert.True(updatedOnboarding.WelcomeEmailSent);
        Assert.True(updatedOnboarding.WelcomeEmailSentAt.HasValue);
        Assert.Equal(OnboardingStep.ProfileSetup, updatedOnboarding.CurrentStep);

        // Verify activity logged
        var activities = await _dbContext.OnboardingActivities
            .Where(a => a.CustomerId == customer.Id)
            .ToListAsync();

        Assert.Contains(activities, a => a.Step == OnboardingStep.Welcome && a.IsCompleted);
    }

    [Fact(DisplayName = "GET_Onboarding_Activities_ShouldReturnActivityHistory")]
    public async Task GET_Onboarding_Activities_ShouldReturnActivityHistory()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Activity History Customer",
            PhoneNumber = "0956789012",
            Email = "activity@test.com",
            CustomerTier = "Bronze",
            LoyaltyPoints = 50,
            IsActive = true,
            TenantId = tenantId
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        // Create onboarding with activities
        var onboarding = new CustomerOnboarding
        {
            CustomerId = customer.Id,
            Status = OnboardingStatus.InProgress,
            CurrentStep = OnboardingStep.AppInstall,
            StartedAt = DateTime.UtcNow.AddDays(-2),
            TenantId = tenantId
        };

        _dbContext.CustomerOnboardings.Add(onboarding);
        await _dbContext.SaveChangesAsync();

        // Add activities
        var activities = new[]
        {
            new OnboardingActivity
            {
                CustomerId = customer.Id,
                Step = OnboardingStep.Welcome,
                Description = "Welcome step completed",
                IsCompleted = true,
                CompletedAt = DateTime.UtcNow.AddDays(-1),
                ActivityDate = DateTime.UtcNow.AddDays(-1)
            },
            new OnboardingActivity
            {
                CustomerId = customer.Id,
                Step = OnboardingStep.ProfileSetup,
                Description = "Profile setup completed",
                IsCompleted = true,
                CompletedAt = DateTime.UtcNow.AddHours(-12),
                ActivityDate = DateTime.UtcNow.AddHours(-12)
            },
            new OnboardingActivity
            {
                CustomerId = customer.Id,
                Step = OnboardingStep.AppInstall,
                Description = "App installation in progress",
                IsCompleted = false,
                ActivityDate = DateTime.UtcNow.AddHours(-2)
            }
        };

        _dbContext.OnboardingActivities.AddRange(activities);
        await _dbContext.SaveChangesAsync();

        SetTenantContext(tenantId);

        // Act
        var response = await _client.GetAsync($"/api/customers/{customer.Id}/onboarding/activities");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<List<OnboardingActivityResponse>>();
        Assert.NotNull(responseContent);
        Assert.Equal(3, responseContent.Count);

        // Verify activities ordered by date (newest first)
        var orderedActivities = responseContent.OrderByDescending(a => a.ActivityDate).ToList();
        Assert.Equal(orderedActivities, responseContent);

        // Verify activity details
        var welcomeActivity = responseContent.FirstOrDefault(a => a.Step == "Welcome");
        Assert.NotNull(welcomeActivity);
        Assert.True(welcomeActivity.IsCompleted);
        Assert.Equal("Welcome step completed", welcomeActivity.Description);

        var appInstallActivity = responseContent.FirstOrDefault(a => a.Step == "AppInstall");
        Assert.NotNull(appInstallActivity);
        Assert.False(appInstallActivity.IsCompleted);
        Assert.Equal("App installation in progress", appInstallActivity.Description);
    }
}

// Supporting classes for API testing
public class CustomerOnboardingResponse
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool HasInstalledApp { get; set; }
    public DateTime? AppInstalledAt { get; set; }
    public string? DeviceType { get; set; }
    public string? AppVersion { get; set; }
    public bool WelcomeEmailSent { get; set; }
    public DateTime? WelcomeEmailSentAt { get; set; }
    public bool LoyaltyProgramActivated { get; set; }
    public DateTime? LoyaltyActivatedAt { get; set; }
}

public class CustomerOnboardingStatusResponse : CustomerOnboardingResponse
{
    public int ProgressPercentage { get; set; }
    public List<string> CompletedSteps { get; set; } = new();
    public List<string> PendingSteps { get; set; } = new();
    public DateTime? EstimatedCompletion { get; set; }
}

public class EmailResponse
{
    public bool Sent { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string? MessageId { get; set; }
    public DateTime? SentAt { get; set; }
}

public class OnboardingActivityResponse
{
    public Guid Id { get; set; }
    public string Step { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime ActivityDate { get; set; }
}
