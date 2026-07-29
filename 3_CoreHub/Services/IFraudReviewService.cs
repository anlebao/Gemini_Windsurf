using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S6 (Sprint 6 v1.2): Fraud review service — admin review queue for FraudFlags.
/// List pending, confirm/dismiss with side effects, 3-strike ban, fraud stats, salesman self-view.
/// </summary>
public interface IFraudReviewService
{
    /// <summary>Get fraud flags by status (default Pending), sorted by RiskScore desc. Paginated.</summary>
    Task<PagedResult<FraudFlagDto>> GetFlagsAsync(string status, int page, int pageSize);

    /// <summary>Get fraud flag detail + related entities.</summary>
    Task<FraudFlagDetailDto?> GetDetailAsync(Guid id);

    /// <summary>Confirm fraud flag. Side effects: reject related entity, wallet reversal if paid, 3-strike ban check.</summary>
    Task<ConfirmResultDto> ConfirmAsync(Guid fraudFlagId, Guid confirmedBy);

    /// <summary>Dismiss fraud flag. Side effects: whitelist device, no strike.</summary>
    Task<DismissResultDto> DismissAsync(Guid fraudFlagId, Guid dismissedBy);

    /// <summary>Get fraud stats dashboard.</summary>
    Task<FraudStatsDto> GetStatsAsync();

    /// <summary>Salesman self-view: get own fraud flags only.</summary>
    Task<List<FraudFlagDto>> GetMyFlagsAsync(Guid customerId);
}

/// <summary>DTO for fraud flag list.</summary>
public class FraudFlagDto
{
    public Guid Id { get; set; }
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public int RiskScore { get; set; }
    public string RiskFactors { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>DTO for fraud flag detail with related entities.</summary>
public class FraudFlagDetailDto : FraudFlagDto
{
    public string Description { get; set; } = string.Empty;
    public string FlagType { get; set; } = string.Empty;
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public FraudDeviceDto? Device { get; set; }
    public FraudSalesReferralDto? SalesReferral { get; set; }
    public FraudAppInstallDto? AppInstallAttribution { get; set; }
}

public class FraudDeviceDto
{
    public Guid Id { get; set; }
    public string FingerprintHash { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsVerified { get; set; }
    public int RiskScore { get; set; }
}

public class FraudSalesReferralDto
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public decimal CommissionAmount { get; set; }
    public string CommissionStatus { get; set; } = string.Empty;
}

public class FraudAppInstallDto
{
    public Guid Id { get; set; }
    public decimal BonusAmount { get; set; }
    public string AttributionStatus { get; set; } = string.Empty;
}

/// <summary>Result of confirm action.</summary>
public class ConfirmResultDto
{
    public string Status { get; set; } = string.Empty;
    public List<string> SideEffects { get; set; } = new();
    public bool CustomerBanned { get; set; }
}

/// <summary>Result of dismiss action.</summary>
public class DismissResultDto
{
    public string Status { get; set; } = string.Empty;
    public List<string> SideEffects { get; set; } = new();
}

/// <summary>Fraud stats dashboard DTO.</summary>
public class FraudStatsDto
{
    public int Pending { get; set; }
    public int Confirmed { get; set; }
    public int Dismissed { get; set; }
    public int Reviewed { get; set; }
    public decimal TotalLossPrevented { get; set; }
    public List<TopFlaggedCustomerDto> TopFlaggedCustomers { get; set; } = new();
}

public class TopFlaggedCustomerDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int FlagCount { get; set; }
}
