using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Json;
using Xunit;
using VanAn.Shared.Domain;
using VanAn.API.Tests.Infrastructure;

namespace VanAn.API.Tests;

/// <summary>
/// API tests for Lead Management
/// Layer 3: API Tests - Lead CRUD Operations
/// </summary>
public class LeadManagementAPITests : ApiTestBase
{
    private readonly HttpClient _client;
    private readonly VanAnDbContext _dbContext;

    public LeadManagementAPITests()
    {
        _client = Factory.CreateClient();
        _dbContext = Factory.Services.GetRequiredService<VanAnDbContext>();
    }

    [Fact(DisplayName = "GET_Leads_Pagination_ShouldReturnCorrectPage")]
    public async Task GET_Leads_Pagination_ShouldReturnCorrectPage()
    {
        // Arrange - Create test leads
        var tenantId = Guid.NewGuid();
        var leads = new List<Lead>();
        
        for (int i = 1; i <= 25; i++)
        {
            leads.Add(new Lead
            {
                FullName = $"Lead {i}",
                PhoneNumber = $"09{i:D8}",
                Status = LeadStatus.New,
                Source = LeadSource.Facebook,
                TenantId = tenantId
            });
        }

        _dbContext.Leads.AddRange(leads);
        await _dbContext.SaveChangesAsync();

        SetTenantContext(tenantId);

        // Act - Get first page
        var response = await _client.GetAsync("/api/leads?page=1&pageSize=10");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<PagedLeadResponse>();
        Assert.NotNull(responseContent);
        Assert.Equal(10, responseContent.Data.Count);
        Assert.Equal(1, responseContent.Page);
        Assert.Equal(10, responseContent.PageSize);
        Assert.Equal(25, responseContent.TotalCount);
        Assert.Equal(3, responseContent.TotalPages);

        // Verify first lead
        var firstLead = responseContent.Data.First();
        Assert.Equal("Lead 1", firstLead.FullName);
        Assert.Equal("09100000001", firstLead.PhoneNumber);
    }

    [Fact(DisplayName = "POST_Lead_ShouldValidateRequiredFields")]
    public async Task POST_Lead_ShouldValidateRequiredFields()
    {
        // Arrange - Missing required fields
        var invalidLead = new
        {
            phone_number = "0987654321",
            // Missing full_name
            email = "test@example.com"
        };

        SetTenantContext(Guid.NewGuid());

        // Act
        var response = await _client.PostAsync("/api/leads", CreateJsonContent(invalidLead));

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(errorResponse);
        Assert.Contains("full_name", errorResponse.Message.ToLower());

        // Verify no data was saved
        var savedLeads = await _dbContext.Leads
            .Where(l => l.PhoneNumber == "0987654321")
            .ToListAsync();

        Assert.Empty(savedLeads);
    }

    [Fact(DisplayName = "PUT_Lead_Status_ShouldTriggerWorkflow")]
    public async Task PUT_Lead_Status_ShouldTriggerWorkflow()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var lead = new Lead
        {
            FullName = "Status Update Test Lead",
            PhoneNumber = "0912345678",
            Status = LeadStatus.New,
            Source = LeadSource.Facebook,
            TenantId = tenantId
        };

        _dbContext.Leads.Add(lead);
        await _dbContext.SaveChangesAsync();

        SetTenantContext(tenantId);

        var statusUpdate = new
        {
            status = "Contacted",
            staff_id = staffId,
            notes = "Contacted via phone call"
        };

        // Act
        var response = await _client.PutAsync($"/api/leads/{lead.Id}/status", 
            CreateJsonContent(statusUpdate));

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<LeadResponse>();
        Assert.NotNull(responseContent);
        Assert.Equal("Contacted", responseContent.Status);
        Assert.Equal(staffId.ToString(), responseContent.AssignedStaffId);

        // Verify in database
        var updatedLead = await _dbContext.Leads.FindAsync(lead.Id);
        Assert.NotNull(updatedLead);
        Assert.Equal(LeadStatus.Contacted, updatedLead.Status);
        Assert.Equal(staffId, updatedLead.AssignedStaffId);
        Assert.True(updatedLead.LastContactDate.HasValue);

        // Verify activity logged
        var activities = await _dbContext.LeadActivities
            .Where(a => a.LeadId == lead.Id)
            .ToListAsync();

        Assert.NotEmpty(activities);
        Assert.Contains(activities, a => a.ActivityType == LeadActivityType.StatusChanged);
    }

    [Fact(DisplayName = "DELETE_Lead_ShouldSoftDelete")]
    public async Task DELETE_Lead_ShouldSoftDelete()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var lead = new Lead
        {
            FullName = "Delete Test Lead",
            PhoneNumber = "0998765432",
            Status = LeadStatus.New,
            Source = LeadSource.Facebook,
            TenantId = tenantId
        };

        _dbContext.Leads.Add(lead);
        await _dbContext.SaveChangesAsync();

        SetTenantContext(tenantId);

        // Act
        var response = await _client.DeleteAsync($"/api/leads/{lead.Id}");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        // Verify soft delete
        var deletedLead = await _dbContext.Leads.FindAsync(lead.Id);
        Assert.NotNull(deletedLead);
        Assert.True(deletedLead.IsDeleted);

        // Verify not returned in queries
        var activeLeads = await _dbContext.Leads
            .Where(l => l.TenantId == tenantId && !l.IsDeleted)
            .ToListAsync();

        Assert.DoesNotContain(activeLeads, l => l.Id == lead.Id);
    }

    [Fact(DisplayName = "GET_Lead_By_Id_ShouldReturnLeadDetails")]
    public async Task GET_Lead_By_Id_ShouldReturnLeadDetails()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var lead = new Lead
        {
            FullName = "Detail Test Lead",
            PhoneNumber = "0976543210",
            Email = "detail@test.com",
            CompanyName = "Detail Test Company",
            Status = LeadStatus.Qualified,
            LeadScore = 85,
            Source = LeadSource.Facebook,
            TenantId = tenantId
        };

        _dbContext.Leads.Add(lead);
        await _dbContext.SaveChangesAsync();

        SetTenantContext(tenantId);

        // Act
        var response = await _client.GetAsync($"/api/leads/{lead.Id}");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<LeadDetailResponse>();
        Assert.NotNull(responseContent);
        Assert.Equal(lead.Id, responseContent.Id);
        Assert.Equal("Detail Test Lead", responseContent.FullName);
        Assert.Equal("0976543210", responseContent.PhoneNumber);
        Assert.Equal("detail@test.com", responseContent.Email);
        Assert.Equal("Detail Test Company", responseContent.CompanyName);
        Assert.Equal("Qualified", responseContent.Status);
        Assert.Equal(85, responseContent.LeadScore);
        Assert.Equal("Facebook", responseContent.Source);
    }

    [Fact(DisplayName = "GET_Lead_Activity_History_ShouldReturnActivities")]
    public async Task GET_Lead_Activity_History_ShouldReturnActivities()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var lead = new Lead
        {
            FullName = "Activity Test Lead",
            PhoneNumber = "0965432109",
            Status = LeadStatus.New,
            Source = LeadSource.Facebook,
            TenantId = tenantId
        };

        _dbContext.Leads.Add(lead);
        await _dbContext.SaveChangesAsync();

        // Add activities
        var activities = new[]
        {
            new LeadActivity
            {
                LeadId = lead.Id,
                ActivityType = LeadActivityType.Created,
                Description = "Lead created from Facebook",
                ActivityDate = DateTime.UtcNow.AddHours(-2)
            },
            new LeadActivity
            {
                LeadId = lead.Id,
                ActivityType = LeadActivityType.ContactAttempt,
                Description = "Phone call attempted",
                ActivityDate = DateTime.UtcNow.AddHours(-1)
            },
            new LeadActivity
            {
                LeadId = lead.Id,
                ActivityType = LeadActivityType.StatusChanged,
                Description = "Status changed to Qualified",
                ActivityDate = DateTime.UtcNow.AddMinutes(-30)
            }
        };

        _dbContext.LeadActivities.AddRange(activities);
        await _dbContext.SaveChangesAsync();

        SetTenantContext(tenantId);

        // Act
        var response = await _client.GetAsync($"/api/leads/{lead.Id}/activities");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<List<LeadActivityResponse>>();
        Assert.NotNull(responseContent);
        Assert.Equal(3, responseContent.Count);

        // Verify activities ordered by date (newest first)
        var orderedActivities = responseContent.OrderByDescending(a => a.ActivityDate).ToList();
        Assert.Equal(orderedActivities, responseContent);

        // Verify activity details
        var createdActivity = responseContent.FirstOrDefault(a => a.ActivityType == "Created");
        Assert.NotNull(createdActivity);
        Assert.Equal("Lead created from Facebook", createdActivity.Description);
    }
}

// Supporting classes for API testing
public class PagedLeadResponse
{
    public List<LeadResponse> Data { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class LeadDetailResponse : LeadResponse
{
    public string? CompanyName { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
    public DateTime? FirstContactDate { get; set; }
    public DateTime? LastContactDate { get; set; }
    public int ContactAttempts { get; set; }
}

public class LeadActivityResponse
{
    public Guid Id { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ActivityDate { get; set; }
    public bool IsCompleted { get; set; }
}

public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
}
