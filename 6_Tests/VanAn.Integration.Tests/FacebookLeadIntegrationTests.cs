using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;
using Xunit;
using Xunit.Abstractions;
using VanAn.Gateway;
using System.Net.Http.Json;
using static VanAn.Integration.Tests.Infrastructure.TestEntityBuilder;

namespace VanAn.Integration.Tests;

/// <summary>
/// Integration tests for Facebook Lead Integration
/// Layer 2: Integration Tests - Facebook Webhook Processing
/// </summary>
[Trait("Category", "Integration")]
public class FacebookLeadIntegrationTests : IntegrationTestBase
{
    private readonly Lazy<IFacebookLeadService> _facebookLeadService;
    private readonly Lazy<ILeadManagementService> _leadManagementService;

    public FacebookLeadIntegrationTests() : base()
    {
        _facebookLeadService = new Lazy<IFacebookLeadService>(() => _serviceProvider.GetRequiredService<IFacebookLeadService>());
        _leadManagementService = new Lazy<ILeadManagementService>(() => _serviceProvider.GetRequiredService<ILeadManagementService>());
    }

    private IFacebookLeadService GetFacebookLeadService() => _facebookLeadService.Value;
    private ILeadManagementService GetLeadManagementService() => _leadManagementService.Value;

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
        var result = await GetFacebookLeadService().ProcessFacebookWebhookAsync(payload);

        // Assert - Verify in database
        var savedLead = await _dbContext.Leads
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
            () => GetFacebookLeadService().ProcessFacebookWebhookAsync(invalidPayload));

        Assert.Contains("required", exception.Message.ToLower());
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
            CreatedTime = DateTime.UtcNow,
            FormData = new
            {
                full_name = "Original Customer",
                phone_number = "0987654321",
                email = "original@test.com",
                company_name = "Original Company"
            }
        };

        await GetFacebookLeadService().ProcessFacebookWebhookAsync(initialPayload);

        // Act - Process same lead again with updated data
        var updatedPayload = new FacebookWebhookPayload
        {
            LeadId = "fb_lead_duplicate_123",
            AdId = "fb_ad_duplicate_456",
            PageId = "fb_page_duplicate_789",
            CampaignId = "fb_campaign_duplicate_101",
            CreatedTime = DateTime.UtcNow,
            FormData = new
            {
                full_name = "Updated Customer",
                phone_number = "0987654321",
                email = "updated@test.com",
                company_name = "Updated Company"
            }
        };

        await GetFacebookLeadService().ProcessFacebookWebhookAsync(updatedPayload);

        // Assert - Verify lead was updated
        var updatedLead = await _dbContext.Leads
            .FirstOrDefaultAsync(l => l.SourceReference == "fb_lead_duplicate_123");

        Assert.NotNull(updatedLead);
        Assert.Equal("Updated Customer", updatedLead.FullName);
        Assert.Equal("updated@test.com", updatedLead.Email);
    }

    [Fact(DisplayName = "FacebookWebhook_BatchProcessing_ShouldHandleMultipleLeads")]
    public async Task FacebookWebhook_BatchProcessing_ShouldHandleMultipleLeads()
    {
        // Arrange - Create multiple payloads
        var payloads = new[]
        {
            new FacebookWebhookPayload
            {
                LeadId = "fb_batch_1",
                AdId = "fb_ad_batch_1",
                PageId = "fb_page_batch_1",
                CampaignId = "fb_campaign_batch_1",
                CreatedTime = DateTime.UtcNow,
                FormData = new
                {
                    full_name = "Batch Customer 1",
                    phone_number = "0901111111",
                    email = "batch1@test.com",
                    company_name = "Batch Company 1"
                }
            },
            new FacebookWebhookPayload
            {
                LeadId = "fb_batch_2",
                AdId = "fb_ad_batch_2",
                PageId = "fb_page_batch_2",
                CampaignId = "fb_campaign_batch_2",
                CreatedTime = DateTime.UtcNow,
                FormData = new
                {
                    full_name = "Batch Customer 2",
                    phone_number = "0902222222",
                    email = "batch2@test.com",
                    company_name = "Batch Company 2"
                }
            },
            new FacebookWebhookPayload
            {
                LeadId = "fb_batch_3",
                AdId = "fb_ad_batch_3",
                PageId = "fb_page_batch_3",
                CampaignId = "fb_campaign_batch_3",
                CreatedTime = DateTime.UtcNow,
                FormData = new
                {
                    full_name = "Batch Customer 3",
                    phone_number = "0903333333",
                    email = "batch3@test.com",
                    company_name = "Batch Company 3"
                }
            }
        };

        // Act - Process all payloads
        var results = new List<bool>();
        foreach (var payload in payloads)
        {
            var result = await GetFacebookLeadService().ProcessFacebookWebhookAsync(payload);
            results.Add(result != null);
        }

        // Assert - All leads created
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r));

        var createdLeads = await _dbContext.Leads
            .Where(l => l.SourceReference.StartsWith("fb_batch_"))
            .ToListAsync();

        Assert.Equal(3, createdLeads.Count);
    }
}
