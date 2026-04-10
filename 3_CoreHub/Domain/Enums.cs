namespace VanAn.CoreHub.Domain;

/// <summary>
/// Lead source enumeration
/// Defines where the lead originated from
/// </summary>
public enum LeadSource
{
    Manual = 1,
    Facebook = 2,
    Website = 3,
    Referral = 4,
    Email = 5,
    Phone = 6
}

/// <summary>
/// Lead status enumeration
/// Defines the current status of a lead in the sales pipeline
/// </summary>
public enum LeadStatus
{
    New = 1,
    Contacted = 2,
    Qualified = 3,
    NotQualified = 4,
    Converted = 5,
    Lost = 6
}

/// <summary>
/// Lead activity type enumeration
/// Defines the types of activities that can be performed on a lead
/// </summary>
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

/// <summary>
/// Onboarding status enumeration
/// Defines the current status of customer onboarding
/// </summary>
public enum OnboardingStatus
{
    NotStarted = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4,
    Skipped = 5
}

/// <summary>
/// Onboarding step enumeration
/// Defines the steps in the customer onboarding process
/// </summary>
public enum OnboardingStep
{
    Welcome = 1,
    ProfileSetup = 2,
    AppInstall = 3,
    FirstOrder = 4,
    LoyaltyActivation = 5,
    Completed = 6
}
