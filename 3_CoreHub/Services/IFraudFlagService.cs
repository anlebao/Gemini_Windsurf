using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S4 (Sprint 4 v1.2): Fraud flag service — create/query/confirm fraud flags.
/// Used by AppInstallAttributionService + SalesmanService when RiskScore >= 60.
/// Admin review queue in Sprint 6.
/// </summary>
public interface IFraudFlagService
{
    /// <summary>
    /// Create a fraud flag for an entity (SalesReferral, AppInstallAttribution, DeviceRegistration, etc.).
    /// </summary>
    Task<FraudFlag> CreateFlagAsync(
        Guid tenantId,
        FraudEntityType entityType,
        Guid entityId,
        Guid? customerId,
        FraudFlagType flagType,
        int riskScore,
        string riskFactors,
        string description);

    /// <summary>
    /// Get pending fraud flags sorted by RiskScore descending.
    /// </summary>
    Task<List<FraudFlag>> GetPendingFlagsAsync();

    /// <summary>
    /// Confirm a fraud flag — sets status Confirmed + updates related entity status to Rejected.
    /// </summary>
    Task ConfirmFlagAsync(Guid flagId, Guid reviewedBy, string note);

    /// <summary>
    /// Dismiss a fraud flag — sets status Dismissed.
    /// </summary>
    Task DismissFlagAsync(Guid flagId, Guid reviewedBy, string note);
}
