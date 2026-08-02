using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Alliance Phase 2A — unit tests for AllianceWalletService.
/// Uses SQLite in-memory via VanAnDbContextTestFactory.
/// ILoyaltyModeResolver is mocked to isolate wallet logic from mode resolution.
/// Spec: docs/specs/loyalty-alliance-spec.md v1.0.
/// </summary>
public class AllianceWalletServiceTests : IDisposable
{
    private readonly TestContextScope _scope;
    private readonly VanAnDbContext _db;
    private readonly Mock<ILoyaltyModeResolver> _modeResolverMock;
    private readonly AllianceWalletService _sut;

    public AllianceWalletServiceTests()
    {
        _scope = VanAnDbContextTestFactory.Create();
        _db = _scope.Context;

        _modeResolverMock = new Mock<ILoyaltyModeResolver>();
        // Default: 100k cap, Alliance mode
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

    // ──────────────────────────────────────────────────────────
    // GetOrCreateWalletAsync
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-AW-1: GetOrCreateWallet — creates wallet when none exists")]
    public async Task GetOrCreateWallet_NewDevice_CreatesWallet()
    {
        var deviceId = Guid.NewGuid();

        AllianceWallet wallet = await _sut.GetOrCreateWalletAsync(deviceId, phoneNumber: "0901234567");

        wallet.Should().NotBeNull();
        wallet.CustomerDeviceId.Should().Be(deviceId);
        wallet.PhoneNumber.Should().Be("0901234567");
        wallet.TotalPointBalance.Should().Be(0);
        wallet.IsActive.Should().BeTrue();

        // Verify persisted
        AllianceWallet? fromDb = await _sut.GetWalletByDeviceIdAsync(deviceId);
        fromDb.Should().NotBeNull();
        fromDb!.Id.Should().Be(wallet.Id);
    }

    [Fact(DisplayName = "LA-AW-2: GetOrCreateWallet — returns existing wallet on second call")]
    public async Task GetOrCreateWallet_ExistingDevice_ReturnsSameWallet()
    {
        var deviceId = Guid.NewGuid();

        AllianceWallet first = await _sut.GetOrCreateWalletAsync(deviceId, phoneNumber: null);
        AllianceWallet second = await _sut.GetOrCreateWalletAsync(deviceId, phoneNumber: "0901234567");

        second.Id.Should().Be(first.Id);
        second.TotalPointBalance.Should().Be(first.TotalPointBalance);
    }

    // ──────────────────────────────────────────────────────────
    // AddPointsAsync
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-AW-3: AddPoints — new wallet creates and adds points + logs EARN transaction")]
    public async Task AddPoints_NewWallet_CreatesWalletAndAddsPoints()
    {
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var (success, newBalance, error) = await _sut.AddPointsAsync(deviceId, tenantId, points: 500, reason: "order completed");

        success.Should().BeTrue();
        newBalance.Should().Be(500);
        error.Should().BeNull();

        // Verify wallet
        AllianceWallet? wallet = await _sut.GetWalletByDeviceIdAsync(deviceId);
        wallet.Should().NotBeNull();
        wallet!.TotalPointBalance.Should().Be(500);

        // Verify transaction log
        IReadOnlyList<AllianceTransaction> txs = await _sut.GetTransactionsAsync(wallet.Id);
        txs.Should().HaveCount(1);
        txs[0].Type.Should().Be(AllianceTransactionType.EARN);
        txs[0].Points.Should().Be(500);
        txs[0].BalanceAfter.Should().Be(500);
        txs[0].TransactionTenantId.Should().Be(tenantId);
        txs[0].Reason.Should().Be("order completed");
    }

    [Fact(DisplayName = "LA-AW-4: AddPoints — exceeds MaxWalletPoints returns error and does not mutate")]
    public async Task AddPoints_ExceedsMaxWallet_ReturnsError()
    {
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _modeResolverMock
            .Setup(m => m.GetEffectiveMaxWalletPointsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(1000);

        // First add 800 (ok, under 1000 cap)
        await _sut.AddPointsAsync(deviceId, tenantId, points: 800, reason: "first");

        // Second add 300 → 800+300=1100 > 1000 → reject
        var (success, newBalance, error) = await _sut.AddPointsAsync(deviceId, tenantId, points: 300, reason: "second");

        success.Should().BeFalse();
        newBalance.Should().Be(800, "balance must not change on rejected add");
        error.Should().Contain("cap exceeded");

        // Verify only 1 transaction logged (the successful one)
        AllianceWallet? wallet = await _sut.GetWalletByDeviceIdAsync(deviceId);
        wallet!.TotalPointBalance.Should().Be(800);
        IReadOnlyList<AllianceTransaction> txs = await _sut.GetTransactionsAsync(wallet.Id);
        txs.Should().HaveCount(1, "rejected add must not log a transaction");
    }

    [Fact(DisplayName = "LA-AW-5: AddPoints — zero or negative points returns error")]
    public async Task AddPoints_NonPositivePoints_ReturnsError()
    {
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var (success, _, error) = await _sut.AddPointsAsync(deviceId, tenantId, points: 0, reason: "test");

        success.Should().BeFalse();
        error.Should().Contain("positive");
    }

    // ──────────────────────────────────────────────────────────
    // DeductPointsAsync
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-AW-6: DeductPoints — sufficient balance deducts and logs REDEEM transaction")]
    public async Task DeductPoints_SufficientBalance_DeductsAndLogs()
    {
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await _sut.AddPointsAsync(deviceId, tenantId, points: 1000, reason: "earn");

        var (success, newBalance, error) = await _sut.DeductPointsAsync(
            deviceId, tenantId, points: 400, reason: "redeemed voucher", voucherCode: "VC-001");

        success.Should().BeTrue();
        newBalance.Should().Be(600);
        error.Should().BeNull();

        // Verify transaction
        AllianceWallet? wallet = await _sut.GetWalletByDeviceIdAsync(deviceId);
        IReadOnlyList<AllianceTransaction> txs = await _sut.GetTransactionsAsync(wallet!.Id);
        txs.Should().HaveCount(2);
        AllianceTransaction redeem = txs.Single(t => t.Type == AllianceTransactionType.REDEEM);
        redeem.Points.Should().Be(-400);
        redeem.BalanceAfter.Should().Be(600);
        redeem.VoucherCode.Should().Be("VC-001");
        redeem.RefundTenantId.Should().Be(tenantId, "Q4: refund returns to tenant where redeem occurred");
    }

    [Fact(DisplayName = "LA-AW-7: DeductPoints — insufficient balance returns error")]
    public async Task DeductPoints_InsufficientBalance_ReturnsError()
    {
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await _sut.AddPointsAsync(deviceId, tenantId, points: 100, reason: "earn");

        var (success, newBalance, error) = await _sut.DeductPointsAsync(deviceId, tenantId, points: 500, reason: "redeem");

        success.Should().BeFalse();
        newBalance.Should().Be(100, "balance must not change on rejected deduct");
        error.Should().Contain("Insufficient");
    }

    [Fact(DisplayName = "LA-AW-8: DeductPoints — wallet not found returns error")]
    public async Task DeductPoints_WalletNotFound_ReturnsError()
    {
        var (success, _, error) = await _sut.DeductPointsAsync(Guid.NewGuid(), Guid.NewGuid(), points: 100, reason: "test");

        success.Should().BeFalse();
        error.Should().Contain("not found");
    }

    // ──────────────────────────────────────────────────────────
    // RefundAsync
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-AW-9: Refund — adds points back and logs ADJUST transaction with refund tenant")]
    public async Task Refund_AddsPointsBackToWallet()
    {
        var deviceId = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        // Earn 1000 at tenant A, redeem 400 at tenant B
        await _sut.AddPointsAsync(deviceId, tenantA, points: 1000, reason: "earn at A");
        await _sut.DeductPointsAsync(deviceId, tenantB, points: 400, reason: "redeem at B", voucherCode: "VC-X");

        // Refund the redeem — Q4: refund attributed to tenant B (where redeem occurred)
        var (success, newBalance, error) = await _sut.RefundAsync(
            deviceId, tenantB, points: 400, reason: "voucher cancelled", voucherCode: "VC-X");

        success.Should().BeTrue();
        newBalance.Should().Be(1000, "1000 - 400 + 400 = 1000");
        error.Should().BeNull();

        // Verify ADJUST transaction
        AllianceWallet? wallet = await _sut.GetWalletByDeviceIdAsync(deviceId);
        IReadOnlyList<AllianceTransaction> txs = await _sut.GetTransactionsAsync(wallet!.Id);
        AllianceTransaction refund = txs.Single(t => t.Type == AllianceTransactionType.ADJUST);
        refund.Points.Should().Be(400);
        refund.BalanceAfter.Should().Be(1000);
        refund.VoucherCode.Should().Be("VC-X");
        refund.TransactionTenantId.Should().Be(tenantB, "Q4: refund attributed to tenant where redeem occurred");
        refund.RefundTenantId.Should().Be(tenantB);
    }

    // ──────────────────────────────────────────────────────────
    // GetTransactionsAsync / GetTransactionsByTenantAsync
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-AW-10: GetTransactions — returns ordered by date descending")]
    public async Task GetTransactions_ReturnsOrderedByDate()
    {
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await _sut.AddPointsAsync(deviceId, tenantId, points: 100, reason: "first");
        await _sut.AddPointsAsync(deviceId, tenantId, points: 200, reason: "second");
        await _sut.AddPointsAsync(deviceId, tenantId, points: 300, reason: "third");

        AllianceWallet? wallet = await _sut.GetWalletByDeviceIdAsync(deviceId);
        IReadOnlyList<AllianceTransaction> txs = await _sut.GetTransactionsAsync(wallet!.Id);

        txs.Should().HaveCount(3);
        // Newest first — TransactionAt is DateTime.UtcNow, may be equal across rapid calls,
        // so verify by reason order (third was added last)
        txs.Select(t => t.Reason).Should().BeEquivalentTo(new[] { "third", "second", "first" },
            "transactions should be ordered newest-first");
    }

    [Fact(DisplayName = "LA-AW-11: GetTransactionsByTenant — filters to specific tenant")]
    public async Task GetTransactionsByTenant_FiltersToSpecificTenant()
    {
        var deviceId = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await _sut.AddPointsAsync(deviceId, tenantA, points: 100, reason: "at A");
        await _sut.AddPointsAsync(deviceId, tenantB, points: 200, reason: "at B");
        await _sut.AddPointsAsync(deviceId, tenantA, points: 50, reason: "at A again");

        AllianceWallet? wallet = await _sut.GetWalletByDeviceIdAsync(deviceId);
        IReadOnlyList<AllianceTransaction> txsA = await _sut.GetTransactionsByTenantAsync(wallet!.Id, tenantA);
        IReadOnlyList<AllianceTransaction> txsB = await _sut.GetTransactionsByTenantAsync(wallet.Id, tenantB);

        txsA.Should().HaveCount(2);
        txsA.All(t => t.TransactionTenantId == tenantA).Should().BeTrue();
        txsB.Should().HaveCount(1);
        txsB[0].TransactionTenantId.Should().Be(tenantB);
    }
}
