using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;
using System.Text.Json;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Points Integrity (Batch 5, T5.1 — decision D2 APPROVED): Alliance wallet consume
/// attribution. When a customer redeems points at a tenant, the consumed points are attributed
/// to the tenant that OWNS them (SourceTenantId), preferring the redeeming tenant's own points
/// first, then FIFO across other tenants. One REDEEM entry per source tenant.
/// Spec: docs/plans/loyalty-integrity-detail-coding-plan.md §Phase 5.
/// </summary>
public class AllianceWalletAttributionTests : IDisposable
{
    private readonly TestContextScope _scope;
    private readonly VanAnDbContext _db;
    private readonly Mock<ILoyaltyModeResolver> _modeResolverMock;
    private readonly AllianceWalletService _sut;

    public AllianceWalletAttributionTests()
    {
        _scope = VanAnDbContextTestFactory.Create();
        _db = _scope.Context;

        _modeResolverMock = new Mock<ILoyaltyModeResolver>();
        _modeResolverMock
            .Setup(m => m.GetEffectiveMaxWalletPointsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(100_000);

        _sut = new AllianceWalletService(
            _db,
            _modeResolverMock.Object,
            natsEventPublisher: null,
            NullLogger<AllianceWalletService>.Instance);
    }

    public void Dispose() => _scope.Dispose();

    private static async Task EarnAsync(AllianceWalletService sut, Guid deviceId, Guid tenantId, int points, string reason)
    {
        var (success, _, error) = await sut.AddPointsAsync(deviceId, tenantId, points, reason);
        success.Should().BeTrue($"EARN {points} at {tenantId} should succeed — {error}");
        // Ensure distinct TransactionAt for deterministic FIFO ordering (DateTime.UtcNow resolution).
        await Task.Delay(5);
    }

    private async Task<Dictionary<Guid, int>> BreakdownAsync(Guid walletId)
    {
        IReadOnlyList<WalletTenantBalance> balances = await _sut.GetTenantBalancesAsync(walletId);
        return balances.ToDictionary(b => b.TenantId, b => b.NetPoints);
    }

    [Fact(DisplayName = "AW-ATT-1 (D2): redeem 80 at B — B's own 50 first, then A's 30 (FIFO); breakdown A=70, B=0")]
    public async Task Redeem_CrossTenant_PrefersCurrentThenFifo()
    {
        var device = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await EarnAsync(_sut, device, tenantA, 100, "earn A"); // earliest
        await EarnAsync(_sut, device, tenantB, 50, "earn B");  // later

        var (success, newBalance, error) = await _sut.DeductPointsAsync(device, tenantB, 80, "redeem at B", "VC-1");

        success.Should().BeTrue();
        newBalance.Should().Be(70);
        error.Should().BeNull();

        AllianceWallet? wallet = await _sut.GetWalletByDeviceIdAsync(device);
        var redeems = await _db.AllianceTransactions
            .Where(t => t.WalletId == wallet!.Id && t.Type == AllianceTransactionType.REDEEM)
            .ToListAsync();

        redeems.Should().HaveCount(2, "one REDEEM entry per source tenant");
        var fromB = redeems.Single(t => t.SourceTenantId == tenantB);
        fromB.Points.Should().Be(-50, "B's own 50 points consumed first (D2)");
        fromB.TransactionTenantId.Should().Be(tenantB);
        var fromA = redeems.Single(t => t.SourceTenantId == tenantA);
        fromA.Points.Should().Be(-30, "remaining 30 consumed from A (FIFO)");
        fromA.TransactionTenantId.Should().Be(tenantB);
        redeems.Should().AllSatisfy(t => t.BalanceAfter.Should().Be(70));
        redeems.Should().AllSatisfy(t => t.RefundTenantId.Should().Be(tenantB, "Q4: refund returns to redeeming tenant"));

        var breakdown = await BreakdownAsync(wallet!.Id);
        breakdown[tenantA].Should().Be(70, "100 earned − 30 consumed");
        breakdown[tenantB].Should().Be(0, "50 earned − 50 consumed");
        breakdown.Values.Sum().Should().Be(70, "Σ netEarn == wallet balance");
    }

    [Fact(DisplayName = "AW-ATT-2 (D2): after cross-tenant redeem, redeem at A consumes A's remaining points first")]
    public async Task Redeem_AtEarningTenant_ConsumesOwnPointsFirst()
    {
        var device = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await EarnAsync(_sut, device, tenantA, 100, "earn A");
        await EarnAsync(_sut, device, tenantB, 50, "earn B");
        await _sut.DeductPointsAsync(device, tenantB, 80, "redeem at B", "VC-1"); // −50 B, −30 A

        var (success, newBalance, _) = await _sut.DeductPointsAsync(device, tenantA, 60, "redeem at A", "VC-2");

        success.Should().BeTrue();
        newBalance.Should().Be(10, "100 + 50 − 80 − 60");

        AllianceWallet? wallet = await _sut.GetWalletByDeviceIdAsync(device);
        var redeems = await _db.AllianceTransactions
            .Where(t => t.WalletId == wallet!.Id && t.Type == AllianceTransactionType.REDEEM)
            .ToListAsync();

        redeems.Should().HaveCount(3);
        var atA = redeems.Single(t => t.TransactionTenantId == tenantA);
        atA.Points.Should().Be(-60);
        atA.SourceTenantId.Should().Be(tenantA, "redeeming at A consumes A's own points first");

        var breakdown = await BreakdownAsync(wallet!.Id);
        breakdown[tenantA].Should().Be(10, "100 − 30 (cross-tenant) − 60 (at A)");
        breakdown[tenantB].Should().Be(0);
    }

    [Fact(DisplayName = "AW-ATT-3: redeem exceeding total balance rejected — no partial deduction, no entries")]
    public async Task Redeem_ExceedsTotal_Rejected()
    {
        var device = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await EarnAsync(_sut, device, tenantA, 100, "earn A");

        var (success, newBalance, error) = await _sut.DeductPointsAsync(device, tenantB, 150, "redeem too much", "VC-X");

        success.Should().BeFalse();
        newBalance.Should().Be(100, "balance unchanged on rejection");
        error.Should().Contain("Insufficient");

        var redeems = await _db.AllianceTransactions
            .Where(t => t.Type == AllianceTransactionType.REDEEM)
            .ToListAsync();
        redeems.Should().BeEmpty();
    }

    [Fact(DisplayName = "AW-ATT-4: idempotency key preserved across multi-source redeem — retry returns cached result")]
    public async Task Redeem_SameIdempotencyKey_NoDoubleDeduction()
    {
        var device = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await EarnAsync(_sut, device, tenantA, 100, "earn A");
        await EarnAsync(_sut, device, tenantB, 50, "earn B");

        const string key = "redeem:VC-IDEM";
        var (success1, balance1, _) = await _sut.DeductPointsAsync(device, tenantB, 80, "redeem at B", "VC-IDEM", key);
        var (success2, balance2, _) = await _sut.DeductPointsAsync(device, tenantB, 80, "redeem at B", "VC-IDEM", key);

        success1.Should().BeTrue();
        success2.Should().BeTrue();
        balance1.Should().Be(balance2).And.Be(70, "retry must return cached balance, not double-deduct");

        var redeems = await _db.AllianceTransactions
            .Where(t => t.Type == AllianceTransactionType.REDEEM)
            .ToListAsync();
        redeems.Should().HaveCount(2, "only one logical redeem — retry did not duplicate entries");
    }

    [Fact(DisplayName = "AW-ATT-5 (D2): prefer current tenant even when another tenant earned earlier (FIFO applies only after current)")]
    public async Task Redeem_PrefersCurrentTenant_OverOlderEarn()
    {
        var device = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await EarnAsync(_sut, device, tenantA, 100, "earn A"); // earned FIRST
        await EarnAsync(_sut, device, tenantB, 50, "earn B");  // earned later

        // Redeem 120 at B: B has only 50, so A must cover the rest — but B's 50 (current tenant)
        // is consumed first DESPITE being earned later (D2: current tenant priority > FIFO).
        var (success, newBalance, _) = await _sut.DeductPointsAsync(device, tenantB, 120, "redeem at B", "VC-3");

        success.Should().BeTrue();
        newBalance.Should().Be(30);

        AllianceWallet? wallet = await _sut.GetWalletByDeviceIdAsync(device);
        var redeems = await _db.AllianceTransactions
            .Where(t => t.WalletId == wallet!.Id && t.Type == AllianceTransactionType.REDEEM)
            .ToListAsync();

        redeems.Should().HaveCount(2);
        var fromB = redeems.Single(t => t.SourceTenantId == tenantB);
        fromB.Points.Should().Be(-50, "current tenant's points consumed first (D2)");
        var fromA = redeems.Single(t => t.SourceTenantId == tenantA);
        fromA.Points.Should().Be(-70, "remaining 70 from A (FIFO)");

        var breakdown = await BreakdownAsync(wallet!.Id);
        breakdown[tenantA].Should().Be(30);
        breakdown[tenantB].Should().Be(0);
    }

    [Fact(DisplayName = "AW-ATT-6: legacy REDEEM without SourceTenantId attributed to its TransactionTenantId (breakdown)")]
    public async Task LegacyRedeem_NoSourceTenant_AttributedToTransactionTenant()
    {
        var device = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await EarnAsync(_sut, device, tenantA, 100, "earn A");

        // Simulate a pre-Batch-5 REDEEM row (SourceTenantId null) — consumed at B, no attribution.
        AllianceWallet? wallet = await _sut.GetWalletByDeviceIdAsync(device);
        wallet!.DeductPoints(40); // the old code deducted the pool without attribution
        var legacy = new AllianceTransaction(
            walletId: wallet.Id,
            transactionTenantId: tenantB,
            type: AllianceTransactionType.REDEEM,
            points: -40,
            balanceAfter: 60,
            reason: "legacy redeem at B");
        _db.AllianceTransactions.Add(legacy);
        await _db.SaveChangesAsync();

        var breakdown = await BreakdownAsync(wallet.Id);
        breakdown[tenantA].Should().Be(100, "legacy REDEEM is NOT charged to A (no SourceTenantId)");
        breakdown[tenantB].Should().Be(-40, "legacy REDEEM charged to its TransactionTenantId (old behavior)");
        breakdown.Values.Sum().Should().Be(60, "Σ netEarn == wallet balance (invariant preserved)");

        // A new redeem at B: B has no positive net → consume from A (FIFO).
        var (success, newBalance, _) = await _sut.DeductPointsAsync(device, tenantB, 30, "redeem at B", "VC-4");
        success.Should().BeTrue();
        newBalance.Should().Be(30);

        var redeems = await _db.AllianceTransactions
            .Where(t => t.WalletId == wallet.Id && t.Type == AllianceTransactionType.REDEEM && t.SourceTenantId.HasValue)
            .ToListAsync();
        var attributed = redeems.Single();
        attributed.SourceTenantId.Should().Be(tenantA);
        attributed.Points.Should().Be(-30);
    }

    // ──────────────────────────────────────────────────────────
    // BUG-1 (RV finding): Alliance sync payload MUST carry the real customerId so the
    // ShopERP LoyaltySyncSubscriber can bootstrap the mirror stub with the SAME customer
    // identity as PG (device-based stub would mismatch POS customers).
    // ──────────────────────────────────────────────────────────

    private sealed class PayloadCapture
    {
        public byte[]? Last { get; set; }
    }

    private (AllianceWalletService sut, PayloadCapture capture) BuildServiceWithCapturingPublisher()
    {
        var capture = new PayloadCapture();
        var natsMock = new Mock<INatsEventPublisher>();
        natsMock
            .Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], CancellationToken>((_, p, _) => capture.Last = p)
            .Returns(Task.CompletedTask);
        var publisher = new LoyaltyBalanceSyncPublisher(
            natsMock.Object, outboxRepository: null, NullLogger<LoyaltyBalanceSyncPublisher>.Instance);
        var sut = new AllianceWalletService(
            _db, _modeResolverMock.Object, natsEventPublisher: null,
            NullLogger<AllianceWalletService>.Instance,
            loyaltyBalanceSyncPublisher: publisher);
        return (sut, capture);
    }

    private static JsonElement ParsePayload(PayloadCapture capture)
    {
        capture.Last.Should().NotBeNull("publisher must have emitted a NATS message");
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(capture.Last!));
        return doc.RootElement.Clone();
    }

    [Fact(DisplayName = "AW-ATT-7 (BUG-1): AddPoints sync payload carries the real customerId (mirror stub bootstrap)")]
    public async Task AddPoints_SyncPayload_CarriesCustomerId()
    {
        var device = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var (sut, capture) = BuildServiceWithCapturingPublisher();
        await sut.AddPointsAsync(device, tenant, 100, "order earn", customerId: customerId);

        JsonElement root = ParsePayload(capture);
        root.GetProperty("customerId").GetString().Should().Be(customerId.ToString(),
            "BUG-1: sync payload must carry the real customerId so the subscriber can create the mirror stub with PG identity");
        root.GetProperty("customerDeviceId").GetString().Should().Be(device.ToString());
        root.GetProperty("tenantId").GetString().Should().Be(tenant.ToString());
    }

    [Fact(DisplayName = "AW-ATT-8 (BUG-1): DeductPoints sync payload carries the real customerId; raw call (no customerId) → Guid.Empty (backward compat)")]
    public async Task DeductPoints_SyncPayload_CarriesCustomerId()
    {
        var device = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var (sut, capture) = BuildServiceWithCapturingPublisher();
        await sut.AddPointsAsync(device, tenant, 100, "earn", customerId: customerId);
        await sut.DeductPointsAsync(device, tenant, 40, "redeem", "VC-1", customerId: customerId);

        JsonElement root = ParsePayload(capture);
        root.GetProperty("customerId").GetString().Should().Be(customerId.ToString(),
            "BUG-1: spend sync payload must carry the real customerId");
        root.GetProperty("type").GetString().Should().Be("REDEEM");

        // Raw wallet call without customerId (e.g. direct internal API, no order context) →
        // Guid.Empty string, subscriber falls back to device matching for existing rows.
        await sut.DeductPointsAsync(device, tenant, 10, "raw redeem", "VC-2");
        JsonElement raw = ParsePayload(capture);
        raw.GetProperty("customerId").GetString().Should().Be(Guid.Empty.ToString(),
            "callers without customerId keep Guid.Empty (device-based fallback on the subscriber)");
    }
}
