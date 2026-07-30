using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.CommunityFundAggregate;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Sprint 7 Q3 — Community fund service. Balance + spend + history.
/// Used by CommunityFundController (SystemAdmin JWT auth).
/// </summary>
public interface ICommunityFundService
{
    /// <summary>Get current balance + total collected + total spent.</summary>
    Task<CommunityFundBalanceDto> GetBalanceAsync();

    /// <summary>Spend from community fund. Creates CommunityFundSpend tx + audit record.</summary>
    Task<CommunityFundSpendResultDto> SpendAsync(decimal amount, string reason, string recipient, Guid approvedBy);

    /// <summary>Get paginated spend history.</summary>
    Task<PagedResult<CommunityFundSpendRecordDto>> GetHistoryAsync(int page, int pageSize);
}

/// <summary>DTO for community fund balance.</summary>
public class CommunityFundBalanceDto
{
    public decimal Balance { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalSpent { get; set; }
}

/// <summary>DTO for spend result.</summary>
public class CommunityFundSpendResultDto
{
    public Guid TransactionId { get; set; }
    public Guid SpendRecordId { get; set; }
    public decimal BalanceAfter { get; set; }
}

/// <summary>DTO for spend history record.</summary>
public class CommunityFundSpendRecordDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public Guid ApprovedBy { get; set; }
    public DateTime SpentAt { get; set; }
    public Guid WalletTransactionId { get; set; }
}
