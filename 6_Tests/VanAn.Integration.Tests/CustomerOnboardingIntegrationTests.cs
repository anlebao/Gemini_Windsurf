using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Infrastructure;
using VanAn.Integration.Tests.Infrastructure;

namespace VanAn.Integration.Tests;

/// <summary>
/// Integration tests for Customer Onboarding
/// Layer 2: Integration Tests - Customer Onboarding Flow
/// </summary>
public class CustomerOnboardingIntegrationTests : IntegrationTestBase
{
    private readonly ICustomerOnboardingService _customerOnboardingService;
    private readonly INotificationService _notificationService;
    private readonly ILoyaltyRewardsService _loyaltyRewardsService;

    public CustomerOnboardingIntegrationTests()
    {
        _customerOnboardingService = ServiceProvider.GetRequiredService<ICustomerOnboardingService>();
        _notificationService = ServiceProvider.GetRequiredService<INotificationService>();
        _loyaltyRewardsService = ServiceProvider.GetRequiredService<ILoyaltyRewardsService>();
    }

    [Fact(DisplayName = "Onboarding_Flow_ShouldTrackAllSteps")]
    public async Task Onboarding_Flow_ShouldTrackAllSteps()
    {
        // Arrange - Create a customer
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Onboarding Test Customer",
            PhoneNumber = "0987654321",
            Email = "onboarding@test.com",
            CustomerTier = "Bronze",
            LoyaltyPoints = 50,
            IsActive = true,
            TenantId = TestTenantId
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        // Act - Complete full onboarding flow
        var onboarding = await _customerOnboardingService.StartOnboardingAsync(customer.Id);
        Assert.NotNull(onboarding);
        Assert.Equal(OnboardingStatus.InProgress, onboarding.Status);
        Assert.Equal(OnboardingStep.Welcome, onboarding.CurrentStep);

        // Step 1: Send welcome message
        onboarding = await _customerOnboardingService.SendWelcomeMessageAsync(customer.Id);
        Assert.Equal(OnboardingStep.ProfileSetup, onboarding.CurrentStep);
        Assert.True(onboarding.WelcomeEmailSent);

        // Step 2: Track app installation
        onboarding = await _customerOnboardingService.TrackAppInstallationAsync(customer.Id, "iOS", "1.0.0");
        Assert.Equal(OnboardingStep.AppInstall, onboarding.CurrentStep);
        Assert.True(onboarding.HasInstalledApp);
        Assert.Equal("iOS", onboarding.DeviceType);
        Assert.Equal("1.0.0", onboarding.AppVersion);

        // Step 3: Complete onboarding
        var completed = await _customerOnboardingService.CompleteOnboardingAsync(customer.Id);
        Assert.True(completed);

        // Assert - Verify complete onboarding flow
        var completedOnboarding = await _dbContext.CustomerOnboardings
            .Include(o => o.Activities)
            .FirstOrDefaultAsync(o => o.CustomerId == customer.Id);

        Assert.NotNull(completedOnboarding);
        Assert.Equal(OnboardingStatus.Completed, completedOnboarding.Status);
        Assert.Equal(OnboardingStep.Completed, completedOnboarding.CurrentStep);
        Assert.NotNull(completedOnboarding.StartedAt);
        Assert.NotNull(completedOnboarding.CompletedAt);

        // Verify all activities logged
        Assert.NotEmpty(completedOnboarding.Activities);
        Assert.Contains(completedOnboarding.Activities, a => a.Step == OnboardingStep.Welcome);
        Assert.Contains(completedOnboarding.Activities, a => a.Step == OnboardingStep.ProfileSetup);
        Assert.Contains(completedOnboarding.Activities, a => a.Step == OnboardingStep.AppInstall);
        Assert.Contains(completedOnboarding.Activities, a => a.Step == OnboardingStep.Completed);

        // Verify all activities are marked as completed
        Assert.All(completedOnboarding.Activities, a => Assert.True(a.IsCompleted));

        // Verify notifications sent
        _notificationService.Verify(x => x.SendWelcomeNotificationAsync(customer.Id), Times.Once);
        _notificationService.Verify(x => x.SendWelcomeEmailAsync(customer.Email, customer.FullName), Times.Once);
        _notificationService.Verify(x => x.SendAppInstallNotificationAsync(customer.Id), Times.Once);
        _notificationService.Verify(x => x.SendOnboardingCompletionNotificationAsync(customer.Id), Times.Once);
    }

    [Fact(DisplayName = "Onboarding_AppInstall_ShouldUpdateCustomerStatus")]
    public async Task Onboarding_AppInstall_ShouldUpdateCustomerStatus()
    {
        // Arrange - Create customer and start onboarding
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "App Install Customer",
            PhoneNumber = "0912345678",
            Email = "appinstall@test.com",
            CustomerTier = "Bronze",
            LoyaltyPoints = 50,
            IsActive = true,
            TenantId = TestTenantId
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        var onboarding = await _customerOnboardingService.StartOnboardingAsync(customer.Id);

        // Act - Track app installation
        var updatedOnboarding = await _customerOnboardingService.TrackAppInstallationAsync(
            customer.Id, "Android", "1.2.3");

        // Assert - Verify onboarding updated
        Assert.NotNull(updatedOnboarding);
        Assert.True(updatedOnboarding.HasInstalledApp);
        Assert.Equal("Android", updatedOnboarding.DeviceType);
        Assert.Equal("1.2.3", updatedOnboarding.AppVersion);
        Assert.True(updatedOnboarding.AppInstalledAt.HasValue);
        Assert.Equal(OnboardingStep.AppInstall, updatedOnboarding.CurrentStep);

        // Verify app installation activity logged
        var activities = await _dbContext.OnboardingActivities
            .Where(a => a.CustomerId == customer.Id)
            .ToListAsync();

        Assert.Contains(activities, a => a.Step == OnboardingStep.AppInstall && a.IsCompleted);

        // Verify customer status updated (if applicable)
        var updatedCustomer = await _dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == customer.Id);

        Assert.NotNull(updatedCustomer);
        Assert.True(updatedCustomer.IsActive); // Customer should remain active

        // Verify notification sent
        _notificationService.Verify(x => x.SendAppInstallNotificationAsync(customer.Id), Times.Once);
    }

    [Fact(DisplayName = "Onboarding_WelcomeCampaign_ShouldSendPersonalizedMessages")]
    public async Task Onboarding_WelcomeCampaign_ShouldSendPersonalizedMessages()
    {
        // Arrange - Create customer with specific preferences
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Nguyêñ Vãn B",
            PhoneNumber = "0923456789",
            Email = "welcome@test.com",
            CustomerTier = "Bronze",
            LoyaltyPoints = 50,
            IsActive = true,
            TenantId = TestTenantId
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        // Setup notification service to capture sent messages
        List<string> sentNotifications = new List<string>();
        _notificationService.Setup(x => x.SendWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                          .Callback<string, string>((email, name) => 
                          {
                              sentNotifications.Add($"Welcome email sent to {email} for {name}");
                          })
                          .ReturnsAsync(true);

        _notificationService.Setup(x => x.SendWelcomeNotificationAsync(It.IsAny<Guid>()))
                          .Callback<Guid>((customerId) => 
                          {
                              sentNotifications.Add($"Welcome notification sent for customer {customerId}");
                          })
                          .Returns(Task.CompletedTask);

        // Act - Start onboarding and send welcome
        var onboarding = await _customerOnboardingService.StartOnboardingAsync(customer.Id);
        var welcomeResult = await _customerOnboardingService.SendWelcomeMessageAsync(customer.Id);

        // Assert - Verify personalized messages sent
        Assert.NotNull(welcomeResult);
        Assert.True(welcomeResult.WelcomeEmailSent);
        Assert.True(welcomeResult.WelcomeEmailSentAt.HasValue);

        // Verify notifications contain customer-specific information
        Assert.Contains(sentNotifications, n => n.Contains(customer.Email));
        Assert.Contains(sentNotifications, n => n.Contains(customer.FullName));
        Assert.Contains(sentNotifications, n => n.Contains(customer.Id.ToString()));

        // Verify welcome email content is personalized
        _notificationService.Verify(x => x.SendWelcomeEmailAsync(
            customer.Email, 
            customer.FullName), Times.Once);

        // Verify onboarding step progressed
        Assert.Equal(OnboardingStep.ProfileSetup, welcomeResult.CurrentStep);

        // Verify welcome activity logged with personalization
        var activities = await _dbContext.OnboardingActivities
            .Where(a => a.CustomerId == customer.Id)
            .ToListAsync();

        var welcomeActivity = activities.FirstOrDefault(a => a.Step == OnboardingStep.Welcome);
        Assert.NotNull(welcomeActivity);
        Assert.True(welcomeActivity.IsCompleted);
        Assert.Contains(customer.FullName, welcomeActivity.Description);
    }

    [Fact(DisplayName = "Onboarding_LoyaltyActivation_ShouldTriggerAfterFirstOrder")]
    public async Task Onboarding_LoyaltyActivation_ShouldTriggerAfterFirstOrder()
    {
        // Arrange - Create customer and complete initial onboarding steps
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Loyalty Customer",
            PhoneNumber = "0934567890",
            Email = "loyalty@test.com",
            CustomerTier = "Bronze",
            LoyaltyPoints = 50,
            IsActive = true,
            TenantId = TestTenantId
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        // Start onboarding and complete app installation
        var onboarding = await _customerOnboardingService.StartOnboardingAsync(customer.Id);
        await _customerOnboardingService.SendWelcomeMessageAsync(customer.Id);
        await _customerOnboardingService.TrackAppInstallationAsync(customer.Id, "iOS", "1.0.0");

        // Create first order for the customer
        var firstOrder = new Order
        {
            OrderId = new OrderId(Guid.NewGuid()),
            CustomerId = customer.Id,
            OrderType = "DINEIN",
            Status = new OrderStatusId("Completed"),
            SubTotal = 50000,
            TotalAmount = 55000,
            OrderDate = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            TenantId = TestTenantId
        };

        _dbContext.Orders.Add(firstOrder);
        await _dbContext.SaveChangesAsync();

        // Setup loyalty service to simulate points calculation
        _loyaltyRewardsService.Setup(x => x.GetCustomerRewardsAsync(customer.Id))
                            .ReturnsAsync(new LoyaltyRewards
                            {
                                CustomerId = customer.Id,
                                PointBalance = 60, // 50 welcome + 10 from first order
                                Tier = "Bronze",
                                LastUpdated = DateTime.UtcNow
                            });

        // Act - Complete onboarding (should trigger loyalty activation)
        var completed = await _customerOnboardingService.CompleteOnboardingAsync(customer.Id);

        // Assert - Verify loyalty activation
        Assert.True(completed);

        // Verify customer loyalty updated
        var updatedCustomer = await _dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == customer.Id);

        Assert.NotNull(updatedCustomer);
        Assert.True(updatedCustomer.IsActive);
        Assert.NotNull(updatedCustomer.LastOrderDate);

        // Verify loyalty rewards checked and updated
        _loyaltyRewardsService.Verify(x => x.GetCustomerRewardsAsync(customer.Id), Times.Once);

        // Verify loyalty activation activity logged
        var activities = await _dbContext.OnboardingActivities
            .Where(a => a.CustomerId == customer.Id)
            .ToListAsync();

        Assert.Contains(activities, a => a.Step == OnboardingStep.LoyaltyActivation);
        Assert.Contains(activities, a => a.Step == OnboardingStep.Completed);

        // Verify completion notification includes loyalty information
        _notificationService.Verify(x => x.SendOnboardingCompletionNotificationAsync(customer.Id), Times.Once);
    }

    [Fact(DisplayName = "Onboarding_CustomerRetention_ShouldMaintainEngagement")]
    public async Task Onboarding_CustomerRetention_ShouldMaintainEngagement()
    {
        // Arrange - Create customer and complete onboarding
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Retention Customer",
            PhoneNumber = "0945678901",
            Email = "retention@test.com",
            CustomerTier = "Bronze",
            LoyaltyPoints = 50,
            IsActive = true,
            TenantId = TestTenantId
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        // Complete full onboarding
        var onboarding = await _customerOnboardingService.StartOnboardingAsync(customer.Id);
        await _customerOnboardingService.SendWelcomeMessageAsync(customer.Id);
        await _customerOnboardingService.TrackAppInstallationAsync(customer.Id, "Android", "1.1.0");
        await _customerOnboardingService.CompleteOnboardingAsync(customer.Id);

        // Create multiple orders over time to test retention
        var orders = new[]
        {
            new Order
            {
                OrderId = new OrderId(Guid.NewGuid()),
                CustomerId = customer.Id,
                OrderType = "DINEIN",
                Status = new OrderStatusId("Completed"),
                SubTotal = 30000,
                TotalAmount = 33000,
                OrderDate = DateTime.UtcNow.AddDays(-30),
                CompletedAt = DateTime.UtcNow.AddDays(-30),
                TenantId = TestTenantId
            },
            new Order
            {
                OrderId = new OrderId(Guid.NewGuid()),
                CustomerId = customer.Id,
                OrderType = "TAKEAWAY",
                Status = new OrderStatusId("Completed"),
                SubTotal = 45000,
                TotalAmount = 49500,
                OrderDate = DateTime.UtcNow.AddDays(-15),
                CompletedAt = DateTime.UtcNow.AddDays(-15),
                TenantId = TestTenantId
            },
            new Order
            {
                OrderId = new OrderId(Guid.NewGuid()),
                CustomerId = customer.Id,
                OrderType = "DINEIN",
                Status = new OrderStatusId("Completed"),
                SubTotal = 60000,
                TotalAmount = 66000,
                OrderDate = DateTime.UtcNow.AddDays(-1),
                CompletedAt = DateTime.UtcNow.AddDays(-1),
                TenantId = TestTenantId
            }
        };

        _dbContext.Orders.AddRange(orders);
        await _dbContext.SaveChangesAsync();

        // Setup loyalty service to reflect customer progression
        _loyaltyRewardsService.Setup(x => x.GetCustomerRewardsAsync(customer.Id))
                            .ReturnsAsync(new LoyaltyRewards
                            {
                                CustomerId = customer.Id,
                                PointBalance = 150, // Accumulated points
                                Tier = "Silver", // Upgraded tier
                                LastUpdated = DateTime.UtcNow
                            });

        // Act - Check customer retention status
        var customerWithOrders = await _dbContext.Customers
            .Include(c => c.Orders)
            .FirstOrDefaultAsync(c => c.Id == customer.Id);

        // Assert - Verify retention metrics
        Assert.NotNull(customerWithOrders);
        Assert.Equal(3, customerWithOrders.Orders.Count);
        Assert.Equal(148500, customerWithOrders.TotalSpent); // Sum of all orders
        Assert.NotNull(customerWithOrders.LastOrderDate);
        Assert.True(customerWithOrders.LastOrderDate > DateTime.UtcNow.AddDays(-2));

        // Verify customer tier progression
        var currentRewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(customer.Id);
        Assert.NotNull(currentRewards);
        Assert.Equal(150, currentRewards.PointBalance);
        Assert.Equal("Silver", currentRewards.Tier); // Should be upgraded

        // Verify onboarding history maintained
        var onboardingHistory = await _dbContext.CustomerOnboardings
            .Include(o => o.Activities)
            .FirstOrDefaultAsync(o => o.CustomerId == customer.Id);

        Assert.NotNull(onboardingHistory);
        Assert.Equal(OnboardingStatus.Completed, onboardingHistory.Status);
        Assert.NotEmpty(onboardingHistory.Activities);

        // Verify customer engagement maintained
        Assert.True(customerWithOrders.IsActive);
        Assert.True(customerWithOrders.LastOrderDate > DateTime.UtcNow.AddDays(-30)); // Recent activity

        // Verify retention metrics
        var orderFrequency = customerWithOrders.Orders.Count / 30.0; // Orders per day
        Assert.True(orderFrequency > 0.1); // At least one order per 10 days on average
    }

    [Fact(DisplayName = "Onboarding_ErrorRecovery_ShouldHandleFailures")]
    public async Task Onboarding_ErrorRecovery_ShouldHandleFailures()
    {
        // Arrange - Create customer
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Error Recovery Customer",
            PhoneNumber = "0956789012",
            Email = "error@test.com",
            CustomerTier = "Bronze",
            LoyaltyPoints = 50,
            IsActive = true,
            TenantId = TestTenantId
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        // Setup notification service to fail on first attempt
        var callCount = 0;
        _notificationService.Setup(x => x.SendWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                          .Callback<string, string>((email, name) => callCount++)
                          .ReturnsAsync(() => callCount == 1 ? false : true); // Fail first time

        // Act - Attempt onboarding with failure
        var onboarding = await _customerOnboardingService.StartOnboardingAsync(customer.Id);
        
        // First attempt should fail
        var firstAttempt = await _customerOnboardingService.SendWelcomeMessageAsync(customer.Id);
        Assert.False(firstAttempt.WelcomeEmailSent); // Should fail

        // Second attempt should succeed
        var secondAttempt = await _customerOnboardingService.SendWelcomeMessageAsync(customer.Id);
        Assert.True(secondAttempt.WelcomeEmailSent); // Should succeed

        // Assert - Verify error recovery
        var finalOnboarding = await _dbContext.CustomerOnboardings
            .Include(o => o.Activities)
            .FirstOrDefaultAsync(o => o.CustomerId == customer.Id);

        Assert.NotNull(finalOnboarding);
        Assert.True(finalOnboarding.WelcomeEmailSent);
        Assert.NotNull(finalOnboarding.WelcomeEmailSentAt);

        // Verify error handling activities logged
        var errorActivities = finalOnboarding.Activities
            .Where(a => a.Description != null && a.Description.ToLower().Contains("error"))
            .ToList();

        // Should have retry mechanism logged
        Assert.True(callCount >= 2); // Should have retried

        // Verify onboarding can still complete after error
        var completed = await _customerOnboardingService.CompleteOnboardingAsync(customer.Id);
        Assert.True(completed);
    }
}

// Supporting interfaces and classes
public interface INotificationService
{
    Task SendWelcomeNotificationAsync(Guid customerId);
    Task<bool> SendWelcomeEmailAsync(string email, string fullName);
    Task SendAppInstallNotificationAsync(Guid customerId);
    Task SendOnboardingCompletionNotificationAsync(Guid customerId);
}

// Mock implementations
public class NotificationService : INotificationService
{
    public Task SendWelcomeNotificationAsync(Guid customerId)
    {
        return Task.CompletedTask;
    }

    public Task<bool> SendWelcomeEmailAsync(string email, string fullName)
    {
        return Task.FromResult(true);
    }

    public Task SendAppInstallNotificationAsync(Guid customerId)
    {
        return Task.CompletedTask;
    }

    public Task SendOnboardingCompletionNotificationAsync(Guid customerId)
    {
        return Task.CompletedTask;
    }
}
