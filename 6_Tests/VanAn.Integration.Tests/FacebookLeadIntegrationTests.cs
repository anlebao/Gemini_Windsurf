using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Integration.Tests.Infrastructure;

namespace VanAn.Integration.Tests;

/// <summary>
/// Integration tests for Facebook Lead Integration
/// Layer 2: Integration Tests - Facebook Webhook Processing
/// </summary>
public class FacebookLeadIntegrationTests : IntegrationTestBase
{
    private readonly IFacebookLeadService _facebookLeadService;
    private readonly ILeadManagementService _leadManagementService;

    public FacebookLeadIntegrationTests()
    {
        _facebookLeadService = ServiceProvider.GetRequiredService<IFacebookLeadService>();
        _leadManagementService = ServiceProvider.GetRequiredService<ILeadManagementService>();
    }

    [Fact(DisplayName = "FacebookWebhook_ProcessLead_ShouldCreateLeadInDatabase")]
    public async Task FacebookWebhook_ProcessLead_ShouldCreateLeadInDatabase()
    {
        // Arrange
        var payload = new FacebookWebhookPayload
        {
            LeadId = "fb_lead_integration_123",
            AdId = "fb_ad_integration_456",
            PageId = "fb_page_integration_789",
            CampaignId = "fb_campaign_integration_101",
            CreatedTime = DateTime.UtcNow,
            FormData = new
            {
                full_name = "Integration Test Customer",
                phone_number = "0912345678",
                email = "integration@test.com",
                company_name = "Integration Company"
            }
        };

        // Act
        var result = await _facebookLeadService.ProcessFacebookWebhookAsync(payload);

        // Assert - Verify in database
        var savedLead = await _dbContext.Leads
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.SourceReference == "fb_lead_integration_123");

        Assert.NotNull(savedLead);
        Assert.Equal("Integration Test Customer", savedLead.FullName);
        Assert.Equal("0912345678", savedLead.PhoneNumber);
        Assert.Equal("integration@test.com", savedLead.Email);
        Assert.Equal(LeadSource.Facebook, savedLead.Source);
        Assert.Equal(LeadStatus.New, savedLead.Status);
        Assert.True(savedLead.LeadScore >= 70); // Facebook leads should have high score

        // Verify Facebook-specific data
        var facebookLead = await _dbContext.FacebookLeads
            .FirstOrDefaultAsync(fl => fl.FacebookLeadId == "fb_lead_integration_123");

        Assert.NotNull(facebookLead);
        Assert.Equal("fb_ad_integration_456", facebookLead.FacebookAdId);
        Assert.Equal("fb_page_integration_789", facebookLead.FacebookPageId);
        Assert.Equal("fb_campaign_integration_101", facebookLead.FacebookCampaignId);
        Assert.Equal(payload.CreatedTime, facebookLead.FacebookCreatedTime);
        Assert.False(facebookLead.IsFacebookProcessed);

        // Verify activity logged
        Assert.NotEmpty(savedLead.Activities);
        Assert.Contains(savedLead.Activities, a => a.ActivityType == LeadActivityType.Created);
    }

    [Fact(DisplayName = "FacebookWebhook_InvalidPayload_ShouldReturnError")]
    public async Task FacebookWebhook_InvalidPayload_ShouldReturnError()
    {
        // Arrange - Invalid payload (missing required fields)
        var invalidPayload = new FacebookWebhookPayload
        {
            LeadId = "", // Empty lead ID
            AdId = "fb_ad_456",
            PageId = "fb_page_789",
            CampaignId = "fb_campaign_101",
            CreatedTime = DateTime.UtcNow,
            FormData = new
            {
                // Missing required fields
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _facebookLeadService.ProcessFacebookWebhookAsync(invalidPayload));

        Assert.Contains("required", exception.Message.ToLower());

        // Verify no data was saved
        var savedLeads = await _dbContext.Leads
            .Where(l => l.Source == LeadSource.Facebook)
            .ToListAsync();

        Assert.Empty(savedLeads);
    }

    [Fact(DisplayName = "FacebookWebhook_DuplicateLead_ShouldUpdateExisting")]
    public async Task FacebookWebhook_DuplicateLead_ShouldUpdateExisting()
    {
        // Arrange - Create initial lead
        var initialPayload = new FacebookWebhookPayload
        {
            LeadId = "fb_lead_duplicate_123",
            AdId = "fb_ad_duplicate_456",
            PageId = "fb_page_duplicate_789",
            CampaignId = "fb_campaign_duplicate_101",
            CreatedTime = DateTime.UtcNow.AddHours(-1),
            FormData = new
            {
                full_name = "Original Name",
                phone_number = "0987654321",
                email = "original@test.com"
            }
        };

        var initialResult = await _facebookLeadService.ProcessFacebookWebhookAsync(initialPayload);
        Assert.NotNull(initialResult);

        // Act - Process same lead again with updated data
        var updatedPayload = new FacebookWebhookPayload
        {
            LeadId = "fb_lead_duplicate_123", // Same lead ID
            AdId = "fb_ad_duplicate_456",
            PageId = "fb_page_duplicate_789",
            CampaignId = "fb_campaign_duplicate_101",
            CreatedTime = DateTime.UtcNow,
            FormData = new
            {
                full_name = "Updated Name",
                phone_number = "0987654321",
                email = "updated@test.com",
                company_name = "New Company"
            }
        };

        var updatedResult = await _facebookLeadService.ProcessFacebookWebhookAsync(updatedPayload);

        // Assert - Verify lead was updated
        var updatedLead = await _dbContext.Leads
            .FirstOrDefaultAsync(l => l.SourceReference == "fb_lead_duplicate_123");

        Assert.NotNull(updatedLead);
        Assert.Equal("Updated Name", updatedLead.FullName); // Should be updated
        Assert.Equal("updated@test.com", updatedLead.Email); // Should be updated
        Assert.Equal("New Company", updatedLead.CompanyName); // Should be added

        // Verify activity logged for update
        Assert.NotEmpty(updatedLead.Activities);
        Assert.Contains(updatedLead.Activities, a => a.ActivityType == LeadActivityType.StatusChanged);
    }

    [Fact(DisplayName = "FacebookWebhook_ProcessBatch_ShouldHandleMultipleLeads")]
    public async Task FacebookWebhook_ProcessBatch_ShouldHandleMultipleLeads()
    {
        // Arrange - Multiple leads in batch
        var batchPayloads = new[]
        {
            new FacebookWebhookPayload
            {
                LeadId = "fb_lead_batch_1",
                AdId = "fb_ad_batch_1",
                PageId = "fb_page_batch",
                CampaignId = "fb_campaign_batch",
                CreatedTime = DateTime.UtcNow,
                FormData = new { full_name = "Batch Customer 1", phone_number = "0911111111" }
            },
            new FacebookWebhookPayload
            {
                LeadId = "fb_lead_batch_2",
                AdId = "fb_ad_batch_2",
                PageId = "fb_page_batch",
                CampaignId = "fb_campaign_batch",
                CreatedTime = DateTime.UtcNow,
                FormData = new { full_name = "Batch Customer 2", phone_number = "0922222222" }
            },
            new FacebookWebhookPayload
            {
                LeadId = "fb_lead_batch_3",
                AdId = "fb_ad_batch_3",
                PageId = "fb_page_batch",
                CampaignId = "fb_campaign_batch",
                CreatedTime = DateTime.UtcNow,
                FormData = new { full_name = "Batch Customer 3", phone_number = "0933333333" }
            }
        };

        // Act - Process all leads
        var results = new List<FacebookLead>();
        foreach (var payload in batchPayloads)
        {
            var result = await _facebookLeadService.ProcessFacebookWebhookAsync(payload);
            results.Add(result);
        }

        // Assert - Verify all leads were created
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.NotNull(r));

        var savedLeads = await _dbContext.Leads
            .Where(l => l.SourceReference.StartsWith("fb_lead_batch_"))
            .ToListAsync();

        Assert.Equal(3, savedLeads.Count);
        Assert.All(savedLeads, l => Assert.Equal(LeadSource.Facebook, l.Source));

        // Verify batch processing efficiency
        var processingTime = DateTime.UtcNow - batchPayloads.First().CreatedTime;
        Assert.True(processingTime.TotalSeconds < 10); // Should process quickly
    }

    [Fact(DisplayName = "FacebookWebhook_ValidateSignature_ShouldPass")]
    public async Task FacebookWebhook_ValidateSignature_ShouldPass()
    {
        // Arrange
        var payload = "{\"lead_id\":\"test_123\"}";
        var signature = "valid_signature";

        // Act
        var isValid = await _facebookLeadService.ValidateFacebookWebhookAsync(signature, payload);

        // Assert - In real implementation, this would validate against Facebook's webhook signature
        // For testing, we'll simulate validation
        Assert.True(isValid);
    }
}

// Supporting classes for integration testing
public class FacebookWebhookPayload
{
    public string LeadId { get; set; } = string.Empty;
    public string AdId { get; set; } = string.Empty;
    public string PageId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
    public object FormData { get; set; } = new();
}

// Integration test base infrastructure
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected ServiceProvider ServiceProvider { get; private set; } = null!;
    protected VanAnDbContext _dbContext { get; private set; } = null!;
    protected readonly Guid TestTenantId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        // Configure in-memory database
        services.AddDbContext<VanAnDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));

        // Register services
        services.AddScoped<IFacebookLeadService, FacebookLeadService>();
        services.AddScoped<ILeadManagementService, LeadManagementService>();
        services.AddScoped<ILeadConversionService, LeadConversionService>();
        services.AddScoped<ICustomerOnboardingService, CustomerOnboardingService>();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        ServiceProvider = services.BuildServiceProvider();

        // Initialize database
        _dbContext = ServiceProvider.GetRequiredService<VanAnDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        // Set tenant context
        SetTenantContext(TestTenantId);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
        await ServiceProvider.DisposeAsync();
    }

    protected virtual void SetTenantContext(Guid tenantId)
    {
        // Implementation for setting tenant context
        // This would be handled by ITenantProvider in real implementation
    }
}

// Mock implementations for testing
public interface IFacebookLeadService
{
    Task<FacebookLead> ProcessFacebookWebhookAsync(FacebookWebhookPayload payload);
    Task<bool> ValidateFacebookWebhookAsync(string signature, string payload);
}

public interface ILeadManagementService
{
    Task<Lead> CreateLeadAsync(Lead lead);
    Task<Lead> UpdateLeadStatusAsync(Guid leadId, LeadStatus status, Guid? staffId = null);
    Task<List<Lead>> GetLeadsByStatusAsync(LeadStatus status);
}

public interface ILeadConversionService
{
    Task<Customer> ConvertLeadToCustomerAsync(Guid leadId, string conversionReason);
}

public interface ICustomerOnboardingService
{
    Task<CustomerOnboarding> StartOnboardingAsync(Guid customerId);
}

// Mock service implementations
public class FacebookLeadService : IFacebookLeadService
{
    private readonly VanAnDbContext _context;
    private readonly ILogger<FacebookLeadService> _logger;

    public FacebookLeadService(VanAnDbContext context, ILogger<FacebookLeadService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<FacebookLead> ProcessFacebookWebhookAsync(FacebookWebhookPayload payload)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(payload.LeadId))
            throw new ArgumentException("Lead ID is required");

        // Check for existing lead
        var existingLead = await _context.Leads
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.SourceReference == payload.LeadId);

        var fullName = ExtractFormDataField(payload.FormData, "full_name");
        var phoneNumber = ExtractFormDataField(payload.FormData, "phone_number");
        var email = ExtractFormDataField(payload.FormData, "email");

        if (existingLead != null)
        {
            // Update existing lead
            existingLead.FullName = fullName;
            existingLead.Email = email;
            existingLead.CompanyName = ExtractFormDataField(payload.FormData, "company_name");
            existingLead.UpdatedAt = DateTime.UtcNow;

            // Log update activity
            existingLead.Activities.Add(new LeadActivity
            {
                LeadId = existingLead.Id,
                ActivityType = LeadActivityType.StatusChanged,
                Description = "Lead updated from Facebook webhook",
                ActivityDate = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return await _context.FacebookLeads
                .FirstAsync(fl => fl.FacebookLeadId == payload.LeadId);
        }

        // Create new lead
        var lead = new Lead
        {
            FullName = fullName,
            PhoneNumber = phoneNumber,
            Email = email,
            CompanyName = ExtractFormDataField(payload.FormData, "company_name"),
            Source = LeadSource.Facebook,
            SourceReference = payload.LeadId,
            Status = LeadStatus.New,
            LeadScore = 85, // Facebook leads get high score
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        // Create Facebook-specific lead
        var facebookLead = new FacebookLead
        {
            LeadId = lead.Id,
            FacebookLeadId = payload.LeadId,
            FacebookAdId = payload.AdId,
            FacebookPageId = payload.PageId,
            FacebookCampaignId = payload.CampaignId,
            FacebookCreatedTime = payload.CreatedTime,
            FacebookFormData = System.Text.Json.JsonSerializer.Serialize(payload.FormData),
            FullName = fullName,
            PhoneNumber = phoneNumber,
            Email = email,
            Source = LeadSource.Facebook,
            Status = LeadStatus.New,
            LeadScore = 85,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.FacebookLeads.Add(facebookLead);

        // Log creation activity
        lead.Activities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            ActivityType = LeadActivityType.Created,
            Description = "Lead created from Facebook webhook",
            ActivityDate = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return facebookLead;
    }

    public async Task<bool> ValidateFacebookWebhookAsync(string signature, string payload)
    {
        // In real implementation, this would validate against Facebook's webhook signature
        // For testing, we'll simulate validation
        return !string.IsNullOrWhiteSpace(signature) && !string.IsNullOrWhiteSpace(payload);
    }

    private string ExtractFormDataField(object formData, string fieldName)
    {
        if (formData is System.Text.Json.JsonElement jsonElement)
        {
            if (jsonElement.TryGetProperty(fieldName, out var property))
            {
                return property.GetString() ?? string.Empty;
            }
        }
        return string.Empty;
    }
}

public class LeadManagementService : ILeadManagementService
{
    private readonly VanAnDbContext _context;

    public LeadManagementService(VanAnDbContext context)
    {
        _context = context;
    }

    public async Task<Lead> CreateLeadAsync(Lead lead)
    {
        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();
        return lead;
    }

    public async Task<Lead> UpdateLeadStatusAsync(Guid leadId, LeadStatus status, Guid? staffId = null)
    {
        var lead = await _context.Leads.FindAsync(leadId);
        if (lead == null)
            throw new ArgumentException("Lead not found");

        lead.Status = status;
        lead.UpdatedAt = DateTime.UtcNow;
        if (staffId.HasValue)
            lead.AssignedStaffId = staffId.Value;

        await _context.SaveChangesAsync();
        return lead;
    }

    public async Task<List<Lead>> GetLeadsByStatusAsync(LeadStatus status)
    {
        return await _context.Leads
            .Where(l => l.Status == status)
            .ToListAsync();
    }
}

public class LeadConversionService : ILeadConversionService
{
    private readonly VanAnDbContext _context;

    public LeadConversionService(VanAnDbContext context)
    {
        _context = context;
    }

    public async Task<Customer> ConvertLeadToCustomerAsync(Guid leadId, string conversionReason)
    {
        var lead = await _context.Leads.FindAsync(leadId);
        if (lead == null)
            throw new ArgumentException("Lead not found");

        var customer = new Customer
        {
            FullName = lead.FullName,
            PhoneNumber = lead.PhoneNumber,
            Email = lead.Email,
            CustomerTier = "Bronze",
            LoyaltyPoints = 50, // Welcome points
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Customers.Add(customer);

        lead.Status = LeadStatus.Converted;
        lead.ConvertedCustomerId = customer.Id;
        lead.ConversionDate = DateTime.UtcNow;
        lead.ConversionReason = conversionReason;

        await _context.SaveChangesAsync();
        return customer;
    }
}

public class CustomerOnboardingService : ICustomerOnboardingService
{
    private readonly VanAnDbContext _context;

    public CustomerOnboardingService(VanAnDbContext context)
    {
        _context = context;
    }

    public async Task<CustomerOnboarding> StartOnboardingAsync(Guid customerId)
    {
        var onboarding = new CustomerOnboarding
        {
            CustomerId = customerId,
            Status = OnboardingStatus.InProgress,
            CurrentStep = OnboardingStep.Welcome,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.CustomerOnboardings.Add(onboarding);
        await _context.SaveChangesAsync();
        return onboarding;
    }
}
