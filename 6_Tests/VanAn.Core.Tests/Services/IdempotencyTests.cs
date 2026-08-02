using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Consistency Fix Phase 0 — idempotency check tests for AllianceWalletService.
/// Verifies that retrying the same operation with the same IdempotencyKey returns cached
/// result without double-processing (no double points, no duplicate AllianceTransaction).
/// Uses SQLite in-memory via VanAnDbContextTestFactory (same pattern as AllianceWalletServiceTests).
/// </summary>
public class IdempotencyTests : IDisposable
{
    private readonly TestContextScope _scope;
    private readonly VanAnDbContext _db;
    private readonly Mock<ILoyaltyModeResolver> _modeResolverMock;
    private readonly AllianceWalletService _sut;

    public IdempotencyTests()
    {
        _scope = VanAnDbContextTestFactory.Create();
        _db = _scope.Context;

        _modeResolverMock = new Mock<ILoyaltyModeResolver>();
        _modeResolverMock.Setup(m => m.GetEffectiveMaxWalletPointsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(100_000);

        _sut = new AllianceWalletService(
            _db,
            _modeResolverMock.Object,
            natsEventPublisher: null,
            NullLogger<AllianceWalletService>.Instance);
    }

    public void Dispose() => _scope.Dispose();

    [Fact(DisplayName = "LC-IDEM-1: AddPoints with same idempotency key — second call returns cached balance, no double points")]
    public async Task AddPoints_SameIdempotencyKey_ReturnsCachedBalance_NoDoublePoints()
    {
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        const string key = "earn:order-123";

        var (success1, balance1, _) = await _sut.AddPointsAsync(deviceId, tenantId, 100, "order complete", idempotencyKey: key);
        var (success2, balance2, _) = await _sut.AddPointsAsync(deviceId, tenantId, 100, "order complete", idempotencyKey: key);

        success1.Should().BeTrue();
        success2.Should().BeTrue();
        balance1.Should().Be(balance2, "second call returns cached balance from first transaction");
        balance1.Should().Be(100, "only 100 points added, not 200");

        var txs = await _db.AllianceTransactions.ToListAsync();
        txs.Should().HaveCount(1, "only one transaction created — no duplicate from retry");
        txs[0].IdempotencyKey.Should().Be(key);
        txs[0].Points.Should().Be(100);
    }

    [Fact(DisplayName = "LC-IDEM-2: AddPoints with different idempotency keys — both processed")]
    public async Task AddPoints_DifferentIdempotencyKeys_BothProcessed()
    {
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await _sut.AddPointsAsync(deviceId, tenantId, 100, "first", idempotencyKey: "earn:order-1");
        var (success, balance, _) = await _sut.AddPointsAsync(deviceId, tenantId, 50, "second", idempotencyKey: "earn:order-2");

        success.Should().BeTrue();
        balance.Should().Be(150, "both calls processed — 100 + 50");

        var txs = await _db.AllianceTransactions.ToListAsync();
        txs.Should().HaveCount(2);
    }

    [Fact(DisplayName = "LC-IDEM-3: AddPoints with null idempotency key — always processed (backward compat)")]
    public async Task AddPoints_NullIdempotencyKey_AlwaysProcessed()
    {
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await _sut.AddPointsAsync(deviceId, tenantId, 100, "no key", idempotencyKey: null);
        await _sut.AddPointsAsync(deviceId, tenantId, 100, "no key again", idempotencyKey: null);

        var txs = await _db.AllianceTransactions.ToListAsync();
        txs.Should().HaveCount(2, "null key = no idempotency check = both processed");
        txs.Should().AllSatisfy(t => t.IdempotencyKey.Should().BeNull());
    }

    [Fact(DisplayName = "LC-IDEM-4: DeductPoints with same idempotency key — second call returns cached balance, no double deduction")]
    public async Task DeductPoints_SameIdempotencyKey_ReturnsCachedBalance_NoDoubleDeduction()
    {
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await _sut.AddPointsAsync(deviceId, tenantId, 500, "initial balance", idempotencyKey: "earn:init");

        const string redeemKey = "redeem:voucher-ABC";
        var (success1, balance1, _) = await _sut.DeductPointsAsync(deviceId, tenantId, 200, "redeem", voucherCode: "ABC", idempotencyKey: redeemKey);
        var (success2, balance2, _) = await _sut.DeductPointsAsync(deviceId, tenantId, 200, "redeem", voucherCode: "ABC", idempotencyKey: redeemKey);

        success1.Should().BeTrue();
        success2.Should().BeTrue();
        balance1.Should().Be(balance2).And.Be(300, "500 - 200 = 300, not 100");

        var redeemTxs = await _db.AllianceTransactions.Where(t => t.Type == AllianceTransactionType.REDEEM).ToListAsync();
        redeemTxs.Should().HaveCount(1, "only one REDEEM transaction — retry did not double-deduct");
    }

    [Fact(DisplayName = "LC-IDEM-5: Refund with same idempotency key — second call returns cached balance, no double refund")]
    public async Task Refund_SameIdempotencyKey_ReturnsCachedBalance_NoDoubleRefund()
    {
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await _sut.AddPointsAsync(deviceId, tenantId, 500, "initial", idempotencyKey: "earn:init");
        await _sut.DeductPointsAsync(deviceId, tenantId, 200, "redeem", idempotencyKey: "redeem:1");

        const string refundKey = "refund:record-1";
        var (success1, balance1, _) = await _sut.RefundAsync(deviceId, tenantId, 200, "cancel", "VOUCHER-1", idempotencyKey: refundKey);
        var (success2, balance2, _) = await _sut.RefundAsync(deviceId, tenantId, 200, "cancel", "VOUCHER-1", idempotencyKey: refundKey);

        success1.Should().BeTrue();
        success2.Should().BeTrue();
        balance1.Should().Be(balance2).And.Be(500, "500 - 200 + 200 = 500, not 700");

        var refundTxs = await _db.AllianceTransactions.Where(t => t.Type == AllianceTransactionType.ADJUST && t.Points > 0).ToListAsync();
        refundTxs.Should().HaveCount(1, "only one refund transaction — retry did not double-refund");
    }
}
