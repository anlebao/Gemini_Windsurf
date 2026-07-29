using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S4 (Sprint 4): App-install attribution service.
/// Tracks app install attributed to salesman via composite referral code.
/// v1.2: risk scoring + FraudFlag integration. v1.4: KHÔNG tạo WalletTransaction (create sau 24h bởi CoolingPeriodJob).
/// </summary>
public interface IAppInstallAttributionService
{
    /// <summary>
    /// Attribute an app install to a salesman via composite referral code.
    /// v1.2: Computes RiskScore + sets AttributionStatus (Pending/Held/Rejected) + creates FraudFlag if RiskScore>=60.
    /// v1.4: KHÔNG tạo WalletTransaction (create sau 24h bởi CoolingPeriodJob hoặc admin approve Sprint 6).
    /// </summary>
    Task<AppInstallAttributionDto?> AttributeInstallAsync(Guid customerId, string referralCode, string? fingerprintHash = null, string? fingerprintSignals = null, string? deviceToken = null);

    /// <summary>
    /// Get app-install attributions by salesman.
    /// </summary>
    Task<List<AppInstallAttributionDto>> GetBySalesmanAsync(Guid salesmanId);
}

public class AppInstallAttributionDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid SalesmanId { get; set; }
    public Guid ProductId { get; set; }
    public decimal BonusAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public string? RiskFactors { get; set; }
    public DateTime? HoldUntil { get; set; }
    public DateTime InstalledAt { get; set; }
}
