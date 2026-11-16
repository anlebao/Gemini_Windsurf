using VanAn.Atrs.Contracts;

namespace VanAn.Atrs.Infra;

/// <summary>
/// ATRS-9 (FR-013): client tới Gateway internal loyalty API — ledger PG là source of truth.
/// atrs KHÔNG kết nối PG loyalty trực tiếp (multi-VPS, ADR-001) — mọi read/adjust
/// đi qua X-Internal-Api-Key (InternalLoyalty:ApiKey, dùng chung "gateway-internal" client).
/// </summary>
public interface ILoyaltyGatewayClient
{
    /// <summary>Transaction history của customer tại tenant (union ledger entries).</summary>
    Task<IReadOnlyList<LoyaltyLedgerEntryDto>> GetEntriesAsync(
        Guid customerId, Guid tenantId, DateTime? fromUtc = null, DateTime? toUtc = null,
        int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Manual adjustment (maker-checker đã enforce ở DisputeCaseManager).
    /// Idempotent qua idempotencyKey = "dispute:{caseId}:adjust:{decisionId}".
    /// Trả về ResolutionReference (new balance / tx ref) khi thành công.
    /// </summary>
    Task<LoyaltyAdjustResult> AdjustPointsAsync(
        Guid customerId, Guid tenantId, int points, string reason,
        string idempotencyKey, CancellationToken ct = default);
}

public sealed record LoyaltyAdjustResult(bool Success, string? ResolutionReference, string? Error);

/// <summary>GatewayBaseUrl chưa cấu hình → mọi call fail rõ ràng (dispute adjust không silent).</summary>
public sealed class UnconfiguredLoyaltyGatewayClient : ILoyaltyGatewayClient
{
    public Task<IReadOnlyList<LoyaltyLedgerEntryDto>> GetEntriesAsync(
        Guid customerId, Guid tenantId, DateTime? fromUtc = null, DateTime? toUtc = null,
        int limit = 100, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LoyaltyLedgerEntryDto>>([]);

    public Task<LoyaltyAdjustResult> AdjustPointsAsync(
        Guid customerId, Guid tenantId, int points, string reason,
        string idempotencyKey, CancellationToken ct = default)
        => Task.FromResult(new LoyaltyAdjustResult(false, null, "Loyalty gateway chưa được cấu hình (Atrs:GatewayBaseUrl)."));
}
