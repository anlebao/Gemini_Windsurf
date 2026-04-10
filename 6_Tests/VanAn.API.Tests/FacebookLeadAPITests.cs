using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Json;
using Xunit;
using VanAn.Shared.Domain;
using VanAn.API.Tests.Infrastructure;

namespace VanAn.API.Tests;

/// <summary>
/// API tests for Facebook Lead Integration
/// Layer 3: API Tests - Facebook Webhook Endpoints
/// </summary>
public class FacebookLeadAPITests : ApiTestBase
{
    private readonly HttpClient _client;
    private readonly VanAnDbContext _dbContext;

    public FacebookLeadAPITests()
    {
        _client = Factory.CreateClient();
        _dbContext = Factory.Services.GetRequiredService<VanAnDbContext>();
    }

    [Fact(DisplayName = "POST_FacebookWebhook_ValidLead_ShouldReturn201")]
    public async Task POST_FacebookWebhook_ValidLead_ShouldReturn201()
    {
        // Arrange
        var payload = new
        {
            lead_id = "fb_lead_api_test_123",
            ad_id = "fb_ad_api_test_456",
            page_id = "fb_page_api_test_789",
            campaign_id = "fb_campaign_api_test_101",
            created_time = DateTime.UtcNow,
            form_data = new
            {
                full_name = "API Test Customer",
                phone_number = "0987654321",
                email = "apitest@example.com",
                company_name = "API Test Company"
            }
        };

        var signature = "valid_signature_for_testing";

        // Act
        var response = await _client.PostAsync("/api/facebook/webhooks/leads", 
            CreateJsonContent(payload));

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<FacebookLeadResponse>();
        Assert.NotNull(responseContent);
        Assert.Equal("fb_lead_api_test_123", responseContent.FacebookLeadId);
        Assert.Equal("API Test Customer", responseContent.FullName);
        Assert.Equal("0987654321", responseContent.PhoneNumber);
        Assert.Equal("apitest@example.com", responseContent.Email);

        // Verify in database
        var savedLead = await _dbContext.Leads
            .FirstOrDefaultAsync(l => l.SourceReference == "fb_lead_api_test_123");

        Assert.NotNull(savedLead);
        Assert.Equal("API Test Customer", savedLead.FullName);
        Assert.Equal(LeadSource.Facebook, savedLead.Source);
        Assert.Equal(LeadStatus.New, savedLead.Status);
    }

    [Fact(DisplayName = "POST_FacebookWebhook_InvalidSignature_ShouldReturn401")]
    public async Task POST_FacebookWebhook_InvalidSignature_ShouldReturn401()
    {
        // Arrange
        var payload = new
        {
            lead_id = "fb_lead_invalid_123",
            ad_id = "fb_ad_invalid_456",
            page_id = "fb_page_invalid_789",
            campaign_id = "fb_campaign_invalid_101",
            created_time = DateTime.UtcNow,
            form_data = new
            {
                full_name = "Invalid Test Customer",
                phone_number = "0911111111"
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/facebook/webhooks/leads");
        request.Content = CreateJsonContent(payload);
        request.Headers.Add("X-Facebook-Signature", "invalid_signature");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);

        // Verify no data was saved
        var savedLeads = await _dbContext.Leads
            .Where(l => l.SourceReference == "fb_lead_invalid_123")
            .ToListAsync();

        Assert.Empty(savedLeads);
    }

    [Fact(DisplayName = "GET_Leads_ByStatus_ShouldReturnFilteredResults")]
    public async Task GET_Leads_ByStatus_ShouldReturnFilteredResults()
    {
        // Arrange - Create test leads
        var tenantId = Guid.NewGuid();
        var leads = new[]
        {
            new Lead
            {
                FullName = "New Lead 1",
                PhoneNumber = "0911111111",
                Status = LeadStatus.New,
                Source = LeadSource.Facebook,
                TenantId = tenantId
            },
            new Lead
            {
                FullName = "Contacted Lead 1",
                PhoneNumber = "0922222222",
                Status = LeadStatus.Contacted,
                Source = LeadSource.Facebook,
                TenantId = tenantId
            },
            new Lead
            {
                FullName = "New Lead 2",
                PhoneNumber = "0933333333",
                Status = LeadStatus.New,
                Source = LeadSource.Facebook,
                TenantId = tenantId
            }
        };

        _dbContext.Leads.AddRange(leads);
        await _dbContext.SaveChangesAsync();

        // Set tenant context
        SetTenantContext(tenantId);

        // Act
        var response = await _client.GetAsync("/api/leads?status=New");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<List<LeadResponse>>();
        Assert.NotNull(responseContent);
        Assert.Equal(2, responseContent.Count);
        Assert.All(responseContent, lead => Assert.Equal("New", lead.Status));
        Assert.DoesNotContain(responseContent, lead => lead.Status == "Contacted");
    }

    [Fact(DisplayName = "PUT_Lead_Assignment_ShouldUpdateStaffAssignment")]
    public async Task PUT_Lead_Assignment_ShouldUpdateStaffAssignment()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var lead = new Lead
        {
            FullName = "Assignment Test Lead",
            PhoneNumber = "0944444444",
            Status = LeadStatus.New,
            Source = LeadSource.Facebook,
            TenantId = tenantId
        };

        _dbContext.Leads.Add(lead);
        await _dbContext.SaveChangesAsync();

        SetTenantContext(tenantId);

        var assignmentRequest = new
        {
            staff_id = staffId,
            notes = "Assigned for follow-up"
        };

        // Act
        var response = await _client.PutAsync($"/api/leads/{lead.Id}/assignment", 
            CreateJsonContent(assignmentRequest));

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<LeadResponse>();
        Assert.NotNull(responseContent);
        Assert.Equal(staffId.ToString(), responseContent.AssignedStaffId);

        // Verify in database
        var updatedLead = await _dbContext.Leads.FindAsync(lead.Id);
        Assert.NotNull(updatedLead);
        Assert.Equal(staffId, updatedLead.AssignedStaffId);
    }

    [Fact(DisplayName = "POST_Lead_Convert_ShouldCreateCustomer")]
    public async Task POST_Lead_Convert_ShouldCreateCustomer()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var lead = new Lead
        {
            FullName = "Conversion Test Lead",
            PhoneNumber = "0955555555",
            Email = "conversion@test.com",
            Status = LeadStatus.Qualified,
            Source = LeadSource.Facebook,
            TenantId = tenantId
        };

        _dbContext.Leads.Add(lead);
        await _dbContext.SaveChangesAsync();

        SetTenantContext(tenantId);

        var conversionRequest = new
        {
            conversion_reason = "High-quality Facebook lead ready for conversion",
            welcome_offer = "20% discount + 50 points"
        };

        // Act
        var response = await _client.PostAsync($"/api/leads/{lead.Id}/convert", 
            CreateJsonContent(conversionRequest));

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.NotNull(responseContent);
        Assert.Equal("Conversion Test Lead", responseContent.FullName);
        Assert.Equal("0955555555", responseContent.PhoneNumber);
        Assert.Equal("conversion@test.com", responseContent.Email);
        Assert.Equal("Bronze", responseContent.CustomerTier);
        Assert.True(responseContent.IsActive);

        // Verify lead status updated
        var updatedLead = await _dbContext.Leads.FindAsync(lead.Id);
        Assert.NotNull(updatedLead);
        Assert.Equal(LeadStatus.Converted, updatedLead.Status);
        Assert.Equal(responseContent.CustomerId, updatedLead.ConvertedCustomerId);

        // Verify customer created
        var customer = await _dbContext.Customers.FindAsync(responseContent.CustomerId);
        Assert.NotNull(customer);
        Assert.Equal("Conversion Test Lead", customer.FullName);
        Assert.Equal(50, customer.LoyaltyPoints); // Welcome points
    }
}

// Supporting classes for API testing
public class FacebookLeadResponse
{
    public string FacebookLeadId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? CompanyName { get; set; }
    public LeadStatus Status { get; set; }
    public int LeadScore { get; set; }
}

public class LeadResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AssignedStaffId { get; set; }
    public int LeadScore { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerResponse
{
    public Guid CustomerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string CustomerTier { get; set; } = string.Empty;
    public int LoyaltyPoints { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

// API test base infrastructure
public abstract class ApiTestBase : IAsyncLifetime
{
    protected WebApplicationFactory<Program> Factory { get; private set; } = null!;
    protected readonly Guid TestTenantId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Configure in-memory database
                    services.AddDbContext<VanAnDbContext>(options =>
                        options.UseSqlite("DataSource=:memory:"));

                    // Register test services
                    services.AddScoped<IFacebookLeadService, FacebookLeadService>();
                    services.AddScoped<ILeadManagementService, LeadManagementService>();
                    services.AddScoped<ILeadConversionService, LeadConversionService>();
                    services.AddScoped<ICustomerOnboardingService, CustomerOnboardingService>();
                });
            });

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
    }

    protected void SetTenantContext(Guid tenantId)
    {
        // Implementation for setting tenant context in API tests
        // This would be handled by ITenantProvider in real implementation
    }

    protected HttpContent CreateJsonContent(object obj)
    {
        return JsonContent.Create(obj);
    }
}
