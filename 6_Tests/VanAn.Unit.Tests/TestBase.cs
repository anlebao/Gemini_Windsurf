using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using VanAn.Unit.Tests.Domain;
using VanAn.Unit.Tests.Repositories;
using VanAn.Unit.Tests.Services;

namespace VanAn.Unit.Tests;

/// <summary>
/// TDD-compliant test base class
/// No infrastructure dependencies, pure domain testing
/// </summary>
public abstract class TestBase
{
    protected Mock<ILogger<T>> CreateLoggerMock<T>() => new();
    
    // Repository mocks
    protected Mock<ILeadRepository> CreateLeadRepositoryMock() => new();
    protected Mock<IFacebookLeadRepository> CreateFacebookLeadRepositoryMock() => new();
    protected Mock<ILeadActivityRepository> CreateLeadActivityRepositoryMock() => new();
    protected Mock<ICustomerRepository> CreateCustomerRepositoryMock() => new();
    protected Mock<ICustomerOnboardingRepository> CreateCustomerOnboardingRepositoryMock() => new();
    protected Mock<IOnboardingActivityRepository> CreateOnboardingActivityRepositoryMock() => new();
    
    // Service mocks
    protected Mock<IFacebookLeadService> CreateFacebookLeadServiceMock() => new();
    protected Mock<ILeadManagementService> CreateLeadManagementServiceMock() => new();
    protected Mock<ILeadConversionService> CreateLeadConversionServiceMock() => new();
    protected Mock<ICustomerOnboardingService> CreateCustomerOnboardingServiceMock() => new();
    protected Mock<ILoyaltyRewardsService> CreateLoyaltyRewardsServiceMock() => new();
    protected Mock<INotificationService> CreateNotificationServiceMock() => new();
    
    // In-memory repositories for integration testing
    protected ILeadRepository CreateInMemoryLeadRepository() => new InMemoryLeadRepository();
    protected IFacebookLeadRepository CreateInMemoryFacebookLeadRepository() => new InMemoryFacebookLeadRepository();
    protected ILeadActivityRepository CreateInMemoryLeadActivityRepository() => new InMemoryLeadActivityRepository();
    protected ICustomerRepository CreateInMemoryCustomerRepository() => new InMemoryCustomerRepository();
    protected ICustomerOnboardingRepository CreateInMemoryCustomerOnboardingRepository() => new InMemoryCustomerOnboardingRepository();
    protected IOnboardingActivityRepository CreateInMemoryOnboardingActivityRepository() => new InMemoryOnboardingActivityRepository();
    
    // Test data factories
    protected Lead CreateTestLead() => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Test Customer",
        PhoneNumber = "0987654321",
        Email = "test@example.com",
        Status = LeadStatus.New,
        Source = LeadSource.Facebook,
        TenantId = Guid.NewGuid()
    };
    
    protected FacebookLead CreateTestFacebookLead() => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Facebook Test Customer",
        PhoneNumber = "0987654321",
        Email = "facebook@test.com",
        FacebookLeadId = "fb_test_123",
        FacebookAdId = "fb_ad_123",
        Status = LeadStatus.New,
        Source = LeadSource.Facebook,
        TenantId = Guid.NewGuid()
    };
    
    protected Customer CreateTestCustomer() => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Test Customer",
        PhoneNumber = "0987654321",
        Email = "customer@test.com",
        CustomerTier = "Bronze",
        LoyaltyPoints = 50,
        IsActive = true,
        TenantId = Guid.NewGuid()
    };
    
    protected CustomerOnboarding CreateTestCustomerOnboarding() => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        Status = OnboardingStatus.NotStarted,
        CurrentStep = OnboardingStep.Welcome,
        TenantId = Guid.NewGuid()
    };
    
    protected FacebookWebhookPayload CreateTestWebhookPayload() => new()
    {
        LeadId = "fb_test_123",
        AdId = "fb_ad_123",
        PageId = "fb_page_123",
        CampaignId = "fb_campaign_123",
        CreatedTime = DateTime.UtcNow,
        FormData = new
        {
            full_name = "Test Customer",
            phone_number = "0987654321",
            email = "test@example.com"
        }
    };
}
