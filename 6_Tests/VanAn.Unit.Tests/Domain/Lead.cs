using VanAn.Shared.Domain;

namespace VanAn.Unit.Tests.Domain;

/// <summary>
/// TDD-compliant Lead domain model
/// Uses Shared Domain for single source of truth
/// </summary>
// REMOVED: Duplicate LeadId - must use Shared Domain
// public record LeadId(Guid Value);

public class Lead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public LeadId LeadId { get; set; } = new LeadId(Guid.NewGuid());
    
    // Lead Source & Tracking
    public LeadSource Source { get; set; } = LeadSource.Manual;
    public string? SourceReference { get; set; }
    
    // Lead Status & Scoring
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public int LeadScore { get; set; } = 0;
    public string? LeadNotes { get; set; }
    
    // Assignment & Management
    public Guid? AssignedStaffId { get; set; }
    public DateTime? FirstContactDate { get; set; }
    public DateTime? LastContactDate { get; set; }
    public int ContactAttempts { get; set; } = 0;
    
    // Conversion Tracking
    public Guid? ConvertedCustomerId { get; set; }
    public DateTime? ConversionDate { get; set; }
    public string? ConversionReason { get; set; }
    
    // Basic Customer Info
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    
    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public Guid TenantId { get; set; }
}

public class FacebookLead : Lead
{
    public string FacebookLeadId { get; set; } = string.Empty;
    public string FacebookAdId { get; set; } = string.Empty;
    public string FacebookPageId { get; set; } = string.Empty;
    public string FacebookCampaignId { get; set; } = string.Empty;
    public DateTime FacebookCreatedTime { get; set; }
    public string FacebookFormData { get; set; } = string.Empty;
    public bool IsFacebookProcessed { get; set; } = false;
    public DateTime? FacebookProcessedAt { get; set; }
}

public class LeadActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeadId { get; set; }
    public LeadActivityType ActivityType { get; set; }
    public string? Description { get; set; }
    public Guid? CreatedByStaffId { get; set; }
    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;
}

// REMOVED: LeadCustomer class - now using production Customer from VanAn.Shared.Domain (single source of truth)
// See: 1_Shared/Domain.cs - Customer class

public class CustomerOnboarding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public OnboardingStatus Status { get; set; } = OnboardingStatus.NotStarted;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public OnboardingStep CurrentStep { get; set; } = OnboardingStep.Welcome;
    
    // App Installation Tracking
    public bool HasInstalledApp { get; set; } = false;
    public DateTime? AppInstalledAt { get; set; }
    public string? AppVersion { get; set; }
    public string? DeviceType { get; set; }
    
    // Communication Tracking
    public bool WelcomeEmailSent { get; set; } = false;
    public DateTime? WelcomeEmailSentAt { get; set; }
    public bool WelcomeSMSsent { get; set; } = false;
    public DateTime? WelcomeSMSsentAt { get; set; }
    
    // Loyalty Program Activation
    public bool LoyaltyProgramActivated { get; set; } = false;
    public DateTime? LoyaltyActivatedAt { get; set; }
    public string? LoyaltyWelcomeOffer { get; set; }
    
    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public Guid TenantId { get; set; }
}

public class OnboardingActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public OnboardingStep Step { get; set; }
    public string? Description { get; set; }
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }
    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;
}

// Enums
public enum LeadSource
{
    Manual = 1,
    Facebook = 2,
    Website = 3,
    Referral = 4,
    Email = 5,
    Phone = 6
}

public enum LeadStatus
{
    New = 1,
    Contacted = 2,
    Qualified = 3,
    NotQualified = 4,
    Converted = 5,
    Lost = 6
}

public enum LeadActivityType
{
    Created = 1,
    ContactAttempt = 2,
    EmailSent = 3,
    PhoneCall = 4,
    Meeting = 5,
    StatusChanged = 6,
    Converted = 7
}

public enum OnboardingStatus
{
    NotStarted = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4,
    Skipped = 5
}

public enum OnboardingStep
{
    Welcome = 1,
    ProfileSetup = 2,
    AppInstall = 3,
    FirstOrder = 4,
    LoyaltyActivation = 5,
    Completed = 6
}
