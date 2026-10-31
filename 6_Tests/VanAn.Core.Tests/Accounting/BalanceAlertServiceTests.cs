using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.GeneralLedger;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using Xunit;

namespace VanAn.Core.Tests.Accounting;

/// <summary>
/// W4 (P1.3 — TT 152/2025): tests cho BalanceAlertService — cảnh báo âm quỹ TK 111
/// / âm kho TK 152·156 / âm tồn nguyên liệu. NON-BLOCKING (trả alert, không throw).
/// Covers: đủ quỹ → no alert · chi vượt quỹ → alert · xuất &gt; tồn → alert ·
/// tenant isolation · flag OFF → no alert.
/// </summary>
public class BalanceAlertServiceTests
{
    private sealed class StubFeatureFlagService : IFeatureFlagService
    {
        private readonly Dictionary<string, bool> _flags = new();
        public void SetFlag(string name, bool enabled) => _flags[name] = enabled;
        public Task<bool> IsEnabledAsync(string featureName, bool defaultWhenMissing = false, CancellationToken ct = default)
            => Task.FromResult(_flags.TryGetValue(featureName, out var v) ? v : defaultWhenMissing);
        public Task<IReadOnlyList<FeatureFlagDto>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<FeatureFlagDto>>([]);
        public Task SetEnabledAsync(string featureName, bool enabled, Guid updatedBy, CancellationToken ct = default)
        {
            _flags[featureName] = enabled;
            return Task.CompletedTask;
        }
    }

    private static BalanceAlertService CreateService(
        IReadOnlyList<AccountBalanceDto> balances,
        StubFeatureFlagService? flags = null,
        Mock<IGeneralLedgerService>? ledgerMock = null)
    {
        var mock = ledgerMock ?? new Mock<IGeneralLedgerService>();
        mock.Setup(g => g.GetBalancesAsync(
                It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balances);
        return new BalanceAlertService(
            mock.Object,
            flags ?? new StubFeatureFlagService(),
            NullLogger<BalanceAlertService>.Instance);
    }

    private static AccountBalanceDto Balance(string code, decimal closing)
        => new(code, null, OpeningBalance: 0, DebitTotal: 0, CreditTotal: 0, ClosingBalance: closing);

    // ---- Cash (TK 111) ----

    [Fact]
    public async Task Cash_SufficientBalance_NoAlert()
    {
        var sut = CreateService([Balance("111", 5_000_000m)]);
        var result = await sut.CheckCashBalanceAsync(new TenantId(Guid.NewGuid()), 2_000_000m);
        Assert.Null(result);
    }

    // ---- W3/D (FR-PAY-006): cashAccountCode — cảnh báo đúng TK tiền (111/112x) ----

    [Fact]
    public async Task Cash_BankAccount_Uses112xBalance_Not111()
    {
        // Quỹ 111 dư 5tr nhưng TGNH 1121 chỉ có 1tr — chi chuyển khoản 2tr → alert TK 1121.
        var sut = CreateService([Balance("111", 5_000_000m), Balance("1121", 1_000_000m)]);
        var result = await sut.CheckCashBalanceAsync(new TenantId(Guid.NewGuid()), 2_000_000m, "1121");
        Assert.NotNull(result);
        Assert.Contains("1121", result!.SubjectLabel);
        Assert.Equal(-1_000_000m, result.ProjectedBalance);
    }

    [Fact]
    public async Task Cash_NullAccountCode_Defaults111()
    {
        var sut = CreateService([Balance("111", 5_000_000m), Balance("1121", 0m)]);
        var result = await sut.CheckCashBalanceAsync(new TenantId(Guid.NewGuid()), 2_000_000m, null);
        Assert.Null(result); // 111 đủ quỹ — không alert dù 1121 âm
    }

    [Fact]
    public async Task Cash_PaymentExceedsBalance_Alert()
    {
        var sut = CreateService([Balance("111", 1_000_000m)]);
        var result = await sut.CheckCashBalanceAsync(new TenantId(Guid.NewGuid()), 3_000_000m);

        Assert.NotNull(result);
        Assert.Equal(BalanceAlertType.CashNegative, result!.AlertType);
        Assert.Equal(1_000_000m, result.CurrentBalance);
        Assert.Equal(-2_000_000m, result.ProjectedBalance);
        Assert.True(result.IsNegative);
        Assert.Contains("111", result.Message);
    }

    [Fact]
    public async Task Cash_ZeroOutflow_NoAlert_NoLedgerCall()
    {
        var ledgerMock = new Mock<IGeneralLedgerService>();
        var sut = CreateService([], ledgerMock: ledgerMock);
        var result = await sut.CheckCashBalanceAsync(new TenantId(Guid.NewGuid()), 0m);
        Assert.Null(result);
        ledgerMock.Verify(g => g.GetBalancesAsync(
            It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Inventory book balance (TK 152/156) ----

    [Fact]
    public async Task Inventory_OutputExceedsBookBalance_Alert()
    {
        var sut = CreateService([Balance("152", 400_000m), Balance("156", 300_000m)]);
        var result = await sut.CheckInventoryBalanceAsync(new TenantId(Guid.NewGuid()), 1_000_000m);

        Assert.NotNull(result);
        Assert.Equal(BalanceAlertType.InventoryNegative, result!.AlertType);
        Assert.Equal(700_000m, result.CurrentBalance);   // 152 + 156
        Assert.Equal(-300_000m, result.ProjectedBalance);
    }

    [Fact]
    public async Task Inventory_SufficientBookBalance_NoAlert()
    {
        var sut = CreateService([Balance("156", 5_000_000m)]);
        var result = await sut.CheckInventoryBalanceAsync(new TenantId(Guid.NewGuid()), 3_000_000m);
        Assert.Null(result);
    }

    // ---- Ingredient stock (quantity, caller-provided — ShopERP SQLite boundary) ----

    [Fact]
    public async Task Stock_OutputExceedsOnHand_Alert()
    {
        var sut = CreateService([]);
        var result = await sut.CheckStockLevelAsync("Cà phê", currentStock: 2m, projectedOutput: 5m);

        Assert.NotNull(result);
        Assert.Equal(BalanceAlertType.IngredientStockNegative, result!.AlertType);
        Assert.Equal(-3m, result.ProjectedBalance);
        Assert.Contains("Cà phê", result.SubjectLabel);
    }

    [Fact]
    public async Task Stock_SufficientOnHand_NoAlert()
    {
        var sut = CreateService([]);
        var result = await sut.CheckStockLevelAsync("Sữa", currentStock: 10m, projectedOutput: 4m);
        Assert.Null(result);
    }

    // ---- Tenant isolation ----

    [Fact]
    public async Task Cash_TenantIsolation_PassesTenantIdToLedger()
    {
        var ledgerMock = new Mock<IGeneralLedgerService>();
        ledgerMock.Setup(g => g.GetBalancesAsync(
                It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Balance("111", 100m)]);
        var sut = CreateService([], ledgerMock: ledgerMock);

        var tenantA = new TenantId(Guid.NewGuid());
        await sut.CheckCashBalanceAsync(tenantA, 500m);

        // Balance query PHẢI filter đúng tenant — không lẫn số dư tenant khác.
        ledgerMock.Verify(g => g.GetBalancesAsync(
            tenantA, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Feature flag ----

    [Fact]
    public async Task FlagOff_NoAlert_EvenWhenNegative()
    {
        var flags = new StubFeatureFlagService();
        flags.SetFlag("Balance_Alert_Enabled", false);
        var sut = CreateService([Balance("111", 0m)], flags);

        Assert.Null(await sut.CheckCashBalanceAsync(new TenantId(Guid.NewGuid()), 9_999m));
        Assert.Null(await sut.CheckInventoryBalanceAsync(new TenantId(Guid.NewGuid()), 9_999m));
        Assert.Null(await sut.CheckStockLevelAsync("X", 0m, 9_999m));
    }

    [Fact]
    public async Task FlagMissing_DefaultsOn_AlertWorks()
    {
        var sut = CreateService([Balance("111", 0m)]);  // no flag set → default ON
        var result = await sut.CheckCashBalanceAsync(new TenantId(Guid.NewGuid()), 1m);
        Assert.NotNull(result);
    }
}
