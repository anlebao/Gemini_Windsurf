using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Domain;

/// <summary>
/// Lead domain entity
/// Represents a potential customer lead in the system
/// </summary>
public class Lead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public LeadId LeadId { get; set; } = new LeadId(Guid.NewGuid());

    // Lead Source & Tracking
    public LeadSource Source { get; set; } = LeadSource.Manual;
    public string? SourceReference { get; set; }

    // Lead Status & Scoring
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public int LeadScore { get; set; }
    public string? LeadNotes { get; set; }

    // Assignment & Management
    public Guid? AssignedStaffId { get; set; }
    public DateTime? FirstContactDate { get; set; }
    public DateTime? LastContactDate { get; set; }
    public int ContactAttempts { get; set; }

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
    public bool IsDeleted { get; set; }
    public Guid TenantId { get; set; }

    // EF Core constructor for materialization
    protected Lead() { }
}

/// <summary>
/// Facebook Lead entity - inherits from Lead
/// Represents leads generated from Facebook Lead Ads
/// </summary>
public class FacebookLead : Lead
{
    public string FacebookLeadId { get; set; } = string.Empty;
    public string FacebookAdId { get; set; } = string.Empty;
    public string FacebookPageId { get; set; } = string.Empty;
    public string FacebookCampaignId { get; set; } = string.Empty;
    public DateTime FacebookCreatedTime { get; set; }
    public string FacebookFormData { get; set; } = string.Empty;
    public bool IsFacebookProcessed { get; set; }
    public DateTime? FacebookProcessedAt { get; set; }

    // EF Core constructor for materialization
    protected FacebookLead() { }
}

/// <summary>
/// Lead Activity tracking entity
/// Records all activities performed on a lead
/// </summary>
public class LeadActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeadId { get; set; }
    public LeadActivityType ActivityType { get; set; }
    public string? Description { get; set; }
    public Guid? CreatedByStaffId { get; set; }
    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

    // EF Core constructor for materialization
    protected LeadActivity() { }
}


/// <summary>
/// Customer Onboarding entity
/// Tracks the onboarding progress for new customers
/// </summary>
public class CustomerOnboarding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public OnboardingStatus Status { get; set; } = OnboardingStatus.NotStarted;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public OnboardingStep CurrentStep { get; set; } = OnboardingStep.Welcome;

    // App Installation Tracking
    public bool HasInstalledApp { get; set; }
    public DateTime? AppInstalledAt { get; set; }
    public string? AppVersion { get; set; }
    public string? DeviceType { get; set; }

    // Communication Tracking
    public bool WelcomeEmailSent { get; set; }
    public DateTime? WelcomeEmailSentAt { get; set; }
    public bool WelcomeSMSsent { get; set; }
    public DateTime? WelcomeSMSsentAt { get; set; }

    // Loyalty Program Activation
    public bool LoyaltyProgramActivated { get; set; }
    public DateTime? LoyaltyActivatedAt { get; set; }
    public string? LoyaltyWelcomeOffer { get; set; }

    // Navigation Properties
    public virtual ICollection<OnboardingActivity> Activities { get; set; } = new List<OnboardingActivity>();

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public Guid TenantId { get; set; }

    // EF Core constructor for materialization
    public CustomerOnboarding() { }
}

/// <summary>
/// Onboarding Activity entity
/// Tracks individual steps in the onboarding process
/// </summary>
public class OnboardingActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public OnboardingStep Step { get; set; }
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

    // EF Core constructor for materialization
    public OnboardingActivity() { }
}
