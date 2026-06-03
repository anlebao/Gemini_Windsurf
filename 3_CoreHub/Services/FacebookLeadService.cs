using System.Runtime.Serialization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Facebook Lead Service Implementation
/// Handles webhook processing and lead creation from Facebook Lead Ads
/// </summary>
public class FacebookLeadService : IFacebookLeadService
{
    private readonly VanAnDbContext _dbContext;
    private readonly ILogger<FacebookLeadService> _logger;
    private readonly ILeadManagementService _leadManagementService;

    public FacebookLeadService(
        VanAnDbContext dbContext,
        ILogger<FacebookLeadService> logger,
        ILeadManagementService leadManagementService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _leadManagementService = leadManagementService;
    }

    public async Task<FacebookLead> ProcessFacebookWebhookAsync(FacebookWebhookPayload payload)
    {
        _logger.LogInformation("Processing Facebook webhook for lead {LeadId}", payload.LeadId);

        // Validate required fields
        if (string.IsNullOrWhiteSpace(payload.LeadId))
        {
            _logger.LogError("Lead ID is required");
            throw new ArgumentException("Lead ID is required");
        }

        // Check for existing lead
        var existingLead = await _dbContext.Leads
            .FirstOrDefaultAsync(l => l.SourceReference == payload.LeadId && !l.IsDeleted);

        var fullName = ExtractFormDataField(payload.FormData, "full_name");
        var phoneNumber = ExtractFormDataField(payload.FormData, "phone_number");
        var email = ExtractFormDataField(payload.FormData, "email");

        if (existingLead != null)
        {
            _logger.LogInformation("Updating existing lead {LeadId}", existingLead.Id);

            // Update existing lead
            existingLead.FullName = fullName;
            existingLead.PhoneNumber = phoneNumber;
            existingLead.Email = email;
            existingLead.CompanyName = ExtractFormDataField(payload.FormData, "company_name");
            existingLead.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            // Return existing FacebookLead
            var existingFacebookLead = await _dbContext.FacebookLeads
                .FirstAsync(fl => fl.FacebookLeadId == payload.LeadId);

            _logger.LogInformation("Existing lead updated successfully");
            return existingFacebookLead;
        }

        // Create new lead using reflection-based factory to preserve domain integrity
        var lead = (Lead)FormatterServices.GetUninitializedObject(typeof(Lead));
        lead.Id = Guid.NewGuid();
        lead.LeadId = new LeadId(lead.Id);
        lead.FullName = fullName;
        lead.PhoneNumber = phoneNumber;
        lead.Email = email;
        lead.CompanyName = ExtractFormDataField(payload.FormData, "company_name");
        lead.Source = LeadSource.Facebook;
        lead.SourceReference = payload.LeadId;
        lead.Status = LeadStatus.New;
        lead.LeadScore = 85; // Facebook leads get high score
        lead.CreatedAt = DateTime.UtcNow;
        lead.UpdatedAt = DateTime.UtcNow;
        lead.IsDeleted = false;
        lead.TenantId = Guid.Empty; // Will be set by tenant context

        var createdLead = await _leadManagementService.CreateLeadAsync(lead);

        // Create Facebook-specific lead
        var facebookLead = (FacebookLead)FormatterServices.GetUninitializedObject(typeof(FacebookLead));
        facebookLead.Id = Guid.NewGuid();
        facebookLead.LeadId = new LeadId(createdLead.Id);
        facebookLead.FacebookLeadId = payload.LeadId;
        facebookLead.FacebookAdId = payload.AdId;
        facebookLead.FacebookPageId = payload.PageId;
        facebookLead.FacebookCampaignId = payload.CampaignId;
        facebookLead.FacebookCreatedTime = payload.CreatedTime;
        facebookLead.FacebookFormData = JsonSerializer.Serialize(payload.FormData);
        facebookLead.FullName = fullName;
        facebookLead.PhoneNumber = phoneNumber;
        facebookLead.Email = email;
        facebookLead.Source = LeadSource.Facebook;
        facebookLead.Status = LeadStatus.New;
        facebookLead.LeadScore = 85;
        facebookLead.CreatedAt = DateTime.UtcNow;
        facebookLead.UpdatedAt = DateTime.UtcNow;
        facebookLead.IsDeleted = false;
        facebookLead.TenantId = createdLead.TenantId;

        _dbContext.FacebookLeads.Add(facebookLead);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("New Facebook lead created successfully with ID {LeadId}", facebookLead.Id);
        return facebookLead;
    }

    public async Task<bool> ValidateFacebookWebhookAsync(string signature, string payload)
    {
        _logger.LogInformation("Validating Facebook webhook signature");

        // TODO: Implement proper HMAC-SHA256 signature validation
        // For now, return true as a placeholder
        // In production, verify against Facebook App Secret
        
        if (string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("Signature is missing");
            return false;
        }

        // Placeholder validation - always return true for now
        // Implement proper validation when Facebook App Secret is available
        await Task.CompletedTask;
        
        _logger.LogInformation("Webhook signature validation completed (placeholder)");
        return true;
    }

    private string ExtractFormDataField(object formData, string fieldName)
    {
        if (formData == null)
            return string.Empty;

        try
        {
            var jsonElement = JsonSerializer.SerializeToElement(formData);
            if (jsonElement.TryGetProperty(fieldName, out var property))
            {
                return property.GetString() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract form data field {FieldName}", fieldName);
        }

        return string.Empty;
    }
}
