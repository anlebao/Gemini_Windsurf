using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using VanAn.Unit.Tests.Domain;
using VanAn.Unit.Tests.Services;
using VanAn.Unit.Tests.Repositories;

namespace VanAn.Unit.Tests;

/// <summary>
/// TDD-compliant Unit tests for Customer Onboarding Service
/// Layer 1: Unit Tests - Customer Onboarding Workflow
/// </summary>
public class TDDCustomerOnboardingServiceTests : TestBase
{
    [Fact(DisplayName = "StartOnboarding_NewCustomer_ShouldCreateOnboardingRecord")]
    public async Task StartOnboarding_NewCustomer_ShouldCreateOnboardingRecord()
    {
        // Arrange
        var loggerMock = CreateLoggerMock<CustomerOnboardingService>();
        var customerRepositoryMock = CreateCustomerRepositoryMock();
        var customerOnboardingRepositoryMock = CreateCustomerOnboardingRepositoryMock();
        var onboardingActivityRepositoryMock = CreateOnboardingActivityRepositoryMock();
        var notificationServiceMock = CreateNotificationServiceMock();
        
        var service = new CustomerOnboardingService(
            loggerMock.Object, 
            customerRepositoryMock.Object, 
            customerOnboardingRepositoryMock.Object,
            onboardingActivityRepositoryMock.Object,
            notificationServiceMock.Object);

        var customerId = Guid.NewGuid();
        var customer = CreateTestCustomer();
        customer.Id = customerId;

        customerRepositoryMock
            .Setup(x => x.GetByIdAsync(customerId))
            .ReturnsAsync(customer);

        customerOnboardingRepositoryMock
            .Setup(x => x.GetByCustomerIdAsync(customerId))
            .ReturnsAsync((CustomerOnboarding?)null);

        customerOnboardingRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<CustomerOnboarding>()))
            .Returns(Task.CompletedTask);

        onboardingActivityRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<OnboardingActivity>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.StartOnboardingAsync(customerId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal(OnboardingStatus.NotStarted, result.Status);
        Assert.Equal(OnboardingStep.Welcome, result.CurrentStep);
        Assert.NotNull(result.StartedAt);

        // Verify repositories were called
        customerRepositoryMock.Verify(x => x.GetByIdAsync(customerId), Times.Once);
        customerOnboardingRepositoryMock.Verify(x => x.GetByCustomerIdAsync(customerId), Times.Once);
        customerOnboardingRepositoryMock.Verify(x => x.AddAsync(It.IsAny<CustomerOnboarding>()), Times.Once);
        onboardingActivityRepositoryMock.Verify(x => x.AddAsync(It.IsAny<OnboardingActivity>()), Times.Once);
    }

    [Fact(DisplayName = "UpdateOnboardingStep_ValidStep_ShouldUpdateAndLogActivity")]
    public async Task UpdateOnboardingStep_ValidStep_ShouldUpdateAndLogActivity()
    {
        // Arrange
        var loggerMock = CreateLoggerMock<CustomerOnboardingService>();
        var customerRepositoryMock = CreateCustomerRepositoryMock();
        var customerOnboardingRepositoryMock = CreateCustomerOnboardingRepositoryMock();
        var onboardingActivityRepositoryMock = CreateOnboardingActivityRepositoryMock();
        var notificationServiceMock = CreateNotificationServiceMock();
        
        var service = new CustomerOnboardingService(
            loggerMock.Object, 
            customerRepositoryMock.Object, 
            customerOnboardingRepositoryMock.Object,
            onboardingActivityRepositoryMock.Object,
            notificationServiceMock.Object);

        var customerId = Guid.NewGuid();
        var newStep = OnboardingStep.AppInstall;
        var existingOnboarding = CreateTestCustomerOnboarding();
        existingOnboarding.CustomerId = customerId;
        existingOnboarding.CurrentStep = OnboardingStep.Welcome;

        customerOnboardingRepositoryMock
            .Setup(x => x.GetByCustomerIdAsync(customerId))
            .ReturnsAsync(existingOnboarding);

        customerOnboardingRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<CustomerOnboarding>()))
            .Returns(Task.CompletedTask);

        onboardingActivityRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<OnboardingActivity>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.UpdateOnboardingStepAsync(customerId, newStep);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(newStep, result.CurrentStep);
        Assert.Equal(OnboardingStatus.InProgress, result.Status);

        // Verify repositories were called
        customerOnboardingRepositoryMock.Verify(x => x.GetByCustomerIdAsync(customerId), Times.Once);
        customerOnboardingRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<CustomerOnboarding>()), Times.Once);
        onboardingActivityRepositoryMock.Verify(x => x.AddAsync(It.IsAny<OnboardingActivity>()), Times.Once);
    }

    [Fact(DisplayName = "TrackAppInstallation_ValidDevice_ShouldUpdateOnboarding")]
    public async Task TrackAppInstallation_ValidDevice_ShouldUpdateOnboarding()
    {
        // Arrange
        var loggerMock = CreateLoggerMock<CustomerOnboardingService>();
        var customerRepositoryMock = CreateCustomerRepositoryMock();
        var customerOnboardingRepositoryMock = CreateCustomerOnboardingRepositoryMock();
        var onboardingActivityRepositoryMock = CreateOnboardingActivityRepositoryMock();
        var notificationServiceMock = CreateNotificationServiceMock();
        
        var service = new CustomerOnboardingService(
            loggerMock.Object, 
            customerRepositoryMock.Object, 
            customerOnboardingRepositoryMock.Object,
            onboardingActivityRepositoryMock.Object,
            notificationServiceMock.Object);

        var customerId = Guid.NewGuid();
        var deviceType = "iOS";
        var appVersion = "1.0.0";
        var existingOnboarding = CreateTestCustomerOnboarding();
        existingOnboarding.CustomerId = customerId;
        existingOnboarding.HasInstalledApp = false;

        customerOnboardingRepositoryMock
            .Setup(x => x.GetByCustomerIdAsync(customerId))
            .ReturnsAsync(existingOnboarding);

        customerOnboardingRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<CustomerOnboarding>()))
            .Returns(Task.CompletedTask);

        onboardingActivityRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<OnboardingActivity>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.TrackAppInstallationAsync(customerId, deviceType, appVersion);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.HasInstalledApp);
        Assert.Equal(deviceType, result.DeviceType);
        Assert.Equal(appVersion, result.AppVersion);
        Assert.NotNull(result.AppInstalledAt);

        // Verify repositories were called
        customerOnboardingRepositoryMock.Verify(x => x.GetByCustomerIdAsync(customerId), Times.Once);
        customerOnboardingRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<CustomerOnboarding>()), Times.Once);
        onboardingActivityRepositoryMock.Verify(x => x.AddAsync(It.IsAny<OnboardingActivity>()), Times.Once);
    }

    [Fact(DisplayName = "SendWelcomeMessage_ValidCustomer_ShouldSendNotification")]
    public async Task SendWelcomeMessage_ValidCustomer_ShouldSendNotification()
    {
        // Arrange
        var loggerMock = CreateLoggerMock<CustomerOnboardingService>();
        var customerRepositoryMock = CreateCustomerRepositoryMock();
        var customerOnboardingRepositoryMock = CreateCustomerOnboardingRepositoryMock();
        var onboardingActivityRepositoryMock = CreateOnboardingActivityRepositoryMock();
        var notificationServiceMock = CreateNotificationServiceMock();
        
        var service = new CustomerOnboardingService(
            loggerMock.Object, 
            customerRepositoryMock.Object, 
            customerOnboardingRepositoryMock.Object,
            onboardingActivityRepositoryMock.Object,
            notificationServiceMock.Object);

        var customerId = Guid.NewGuid();
        var customer = CreateTestCustomer();
        customer.Id = customerId;
        var existingOnboarding = CreateTestCustomerOnboarding();
        existingOnboarding.CustomerId = customerId;
        existingOnboarding.WelcomeEmailSent = false;

        customerRepositoryMock
            .Setup(x => x.GetByIdAsync(customerId))
            .ReturnsAsync(customer);

        customerOnboardingRepositoryMock
            .Setup(x => x.GetByCustomerIdAsync(customerId))
            .ReturnsAsync(existingOnboarding);

        customerOnboardingRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<CustomerOnboarding>()))
            .Returns(Task.CompletedTask);

        notificationServiceMock
            .Setup(x => x.SendWelcomeNotificationAsync(customerId))
            .Returns(Task.CompletedTask);

        notificationServiceMock
            .Setup(x => x.SendWelcomeEmailAsync(customer.Email, customer.FullName))
            .ReturnsAsync(true);

        onboardingActivityRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<OnboardingActivity>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.SendWelcomeMessageAsync(customerId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.WelcomeEmailSent);
        Assert.NotNull(result.WelcomeEmailSentAt);

        // Verify repositories were called
        customerRepositoryMock.Verify(x => x.GetByIdAsync(customerId), Times.Once);
        customerOnboardingRepositoryMock.Verify(x => x.GetByCustomerIdAsync(customerId), Times.Once);
        customerOnboardingRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<CustomerOnboarding>()), Times.Once);
        notificationServiceMock.Verify(x => x.SendWelcomeNotificationAsync(customerId), Times.Once);
        notificationServiceMock.Verify(x => x.SendWelcomeEmailAsync(customer.Email, customer.FullName), Times.Once);
    }

    [Fact(DisplayName = "CompleteOnboarding_ValidCustomer_ShouldMarkAsCompleted")]
    public async Task CompleteOnboarding_ValidCustomer_ShouldMarkAsCompleted()
    {
        // Arrange
        var loggerMock = CreateLoggerMock<CustomerOnboardingService>();
        var customerRepositoryMock = CreateCustomerRepositoryMock();
        var customerOnboardingRepositoryMock = CreateCustomerOnboardingRepositoryMock();
        var onboardingActivityRepositoryMock = CreateOnboardingActivityRepositoryMock();
        var notificationServiceMock = CreateNotificationServiceMock();
        
        var service = new CustomerOnboardingService(
            loggerMock.Object, 
            customerRepositoryMock.Object, 
            customerOnboardingRepositoryMock.Object,
            onboardingActivityRepositoryMock.Object,
            notificationServiceMock.Object);

        var customerId = Guid.NewGuid();
        var existingOnboarding = CreateTestCustomerOnboarding();
        existingOnboarding.CustomerId = customerId;
        existingOnboarding.Status = OnboardingStatus.InProgress;
        existingOnboarding.CurrentStep = OnboardingStep.LoyaltyActivation;

        customerOnboardingRepositoryMock
            .Setup(x => x.GetByCustomerIdAsync(customerId))
            .ReturnsAsync(existingOnboarding);

        customerOnboardingRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<CustomerOnboarding>()))
            .Returns(Task.CompletedTask);

        notificationServiceMock
            .Setup(x => x.SendOnboardingCompletionNotificationAsync(customerId))
            .Returns(Task.CompletedTask);

        onboardingActivityRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<OnboardingActivity>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.CompleteOnboardingAsync(customerId);

        // Assert
        Assert.True(result);

        // Verify repositories were called
        customerOnboardingRepositoryMock.Verify(x => x.GetByCustomerIdAsync(customerId), Times.Once);
        customerOnboardingRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<CustomerOnboarding>()), Times.Once);
        notificationServiceMock.Verify(x => x.SendOnboardingCompletionNotificationAsync(customerId), Times.Once);
        onboardingActivityRepositoryMock.Verify(x => x.AddAsync(It.IsAny<OnboardingActivity>()), Times.Once);
    }
}

// TDD-compliant implementation (will fail initially - RED phase)
public class CustomerOnboardingService : ICustomerOnboardingService
{
    private readonly ILogger<CustomerOnboardingService> _logger;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerOnboardingRepository _customerOnboardingRepository;
    private readonly IOnboardingActivityRepository _onboardingActivityRepository;
    private readonly INotificationService _notificationService;

    public CustomerOnboardingService(
        ILogger<CustomerOnboardingService> logger,
        ICustomerRepository customerRepository,
        ICustomerOnboardingRepository customerOnboardingRepository,
        IOnboardingActivityRepository onboardingActivityRepository,
        INotificationService notificationService)
    {
        _logger = logger;
        _customerRepository = customerRepository;
        _customerOnboardingRepository = customerOnboardingRepository;
        _onboardingActivityRepository = onboardingActivityRepository;
        _notificationService = notificationService;
    }

    public async Task<CustomerOnboarding> StartOnboardingAsync(Guid customerId)
    {
        // GREEN PHASE: Full implementation
        _logger.LogInformation("Starting onboarding for customer: {CustomerId}", customerId);
        
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer == null)
        {
            throw new ArgumentException($"Customer not found: {customerId}");
        }

        // Check if onboarding already exists
        var existingOnboarding = await _customerOnboardingRepository.GetByCustomerIdAsync(customerId);
        if (existingOnboarding != null)
        {
            _logger.LogInformation("Onboarding already exists for customer: {CustomerId}", customerId);
            return existingOnboarding;
        }

        // Create new onboarding
        var onboarding = new CustomerOnboarding
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OnboardingStatus.NotStarted,
            CurrentStep = OnboardingStep.Welcome,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TenantId = customer.TenantId
        };

        await _customerOnboardingRepository.AddAsync(onboarding);

        // Log activity
        var activity = new OnboardingActivity
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Step = OnboardingStep.Welcome,
            Description = "Onboarding started",
            ActivityDate = DateTime.UtcNow
        };

        await _onboardingActivityRepository.AddAsync(activity);

        _logger.LogInformation("Onboarding started for customer: {CustomerId}", customerId);
        return onboarding;
    }

    public async Task<CustomerOnboarding> UpdateOnboardingStepAsync(Guid customerId, OnboardingStep step)
    {
        // GREEN PHASE: Full implementation
        _logger.LogInformation("Updating onboarding step for customer: {CustomerId} to {Step}", customerId, step);
        
        var onboarding = await _customerOnboardingRepository.GetByCustomerIdAsync(customerId);
        if (onboarding == null)
        {
            throw new ArgumentException($"Onboarding not found for customer: {customerId}");
        }

        var oldStep = onboarding.CurrentStep;
        onboarding.CurrentStep = step;
        onboarding.Status = OnboardingStatus.InProgress;
        onboarding.UpdatedAt = DateTime.UtcNow;

        await _customerOnboardingRepository.UpdateAsync(onboarding);

        // Log activity
        var activity = new OnboardingActivity
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Step = step,
            Description = $"Step updated from {oldStep} to {step}",
            ActivityDate = DateTime.UtcNow
        };

        await _onboardingActivityRepository.AddAsync(activity);

        _logger.LogInformation("Onboarding step updated: {CustomerId} -> {Step}", customerId, step);
        return onboarding;
    }

    public async Task<CustomerOnboarding> TrackAppInstallationAsync(Guid customerId, string deviceType, string appVersion)
    {
        // GREEN PHASE: Full implementation
        _logger.LogInformation("Tracking app installation for customer: {CustomerId}", customerId);
        
        var onboarding = await _customerOnboardingRepository.GetByCustomerIdAsync(customerId);
        if (onboarding == null)
        {
            throw new ArgumentException($"Onboarding not found for customer: {customerId}");
        }

        onboarding.HasInstalledApp = true;
        onboarding.AppInstalledAt = DateTime.UtcNow;
        onboarding.DeviceType = deviceType;
        onboarding.AppVersion = appVersion;
        onboarding.UpdatedAt = DateTime.UtcNow;

        await _customerOnboardingRepository.UpdateAsync(onboarding);

        // Log activity
        var activity = new OnboardingActivity
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Step = OnboardingStep.AppInstall,
            Description = $"App installed on {deviceType} version {appVersion}",
            ActivityDate = DateTime.UtcNow,
            IsCompleted = true
        };

        await _onboardingActivityRepository.AddAsync(activity);

        // Send notification
        await _notificationService.SendAppInstallNotificationAsync(customerId);

        _logger.LogInformation("App installation tracked: {CustomerId}", customerId);
        return onboarding;
    }

    public async Task<CustomerOnboarding> SendWelcomeMessageAsync(Guid customerId)
    {
        // GREEN PHASE: Full implementation
        _logger.LogInformation("Sending welcome message to customer: {CustomerId}", customerId);
        
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer == null)
        {
            throw new ArgumentException($"Customer not found: {customerId}");
        }

        var onboarding = await _customerOnboardingRepository.GetByCustomerIdAsync(customerId);
        if (onboarding == null)
        {
            throw new ArgumentException($"Onboarding not found for customer: {customerId}");
        }

        // Send welcome notification
        await _notificationService.SendWelcomeNotificationAsync(customerId);

        // Send welcome email
        var emailSent = await _notificationService.SendWelcomeEmailAsync(customer.Email, customer.FullName);

        // Update onboarding
        onboarding.WelcomeEmailSent = emailSent;
        onboarding.WelcomeEmailSentAt = DateTime.UtcNow;
        onboarding.UpdatedAt = DateTime.UtcNow;

        await _customerOnboardingRepository.UpdateAsync(onboarding);

        // Log activity
        var activity = new OnboardingActivity
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Step = OnboardingStep.Welcome,
            Description = $"Welcome message sent (Email: {emailSent})",
            ActivityDate = DateTime.UtcNow,
            IsCompleted = emailSent
        };

        await _onboardingActivityRepository.AddAsync(activity);

        _logger.LogInformation("Welcome message sent: {CustomerId}", customerId);
        return onboarding;
    }

    public async Task<bool> CompleteOnboardingAsync(Guid customerId)
    {
        // GREEN PHASE: Full implementation
        _logger.LogInformation("Completing onboarding for customer: {CustomerId}", customerId);
        
        var onboarding = await _customerOnboardingRepository.GetByCustomerIdAsync(customerId);
        if (onboarding == null)
        {
            throw new ArgumentException($"Onboarding not found for customer: {customerId}");
        }

        onboarding.Status = OnboardingStatus.Completed;
        onboarding.CurrentStep = OnboardingStep.Completed;
        onboarding.CompletedAt = DateTime.UtcNow;
        onboarding.UpdatedAt = DateTime.UtcNow;

        await _customerOnboardingRepository.UpdateAsync(onboarding);

        // Log activity
        var activity = new OnboardingActivity
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Step = OnboardingStep.Completed,
            Description = "Onboarding completed",
            ActivityDate = DateTime.UtcNow,
            IsCompleted = true
        };

        await _onboardingActivityRepository.AddAsync(activity);

        // Send completion notification
        await _notificationService.SendOnboardingCompletionNotificationAsync(customerId);

        _logger.LogInformation("Onboarding completed: {CustomerId}", customerId);
        return true;
    }
}
