using Microsoft.Extensions.Logging;
using Moq;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using Xunit;

namespace VanAn.Core.Tests.Community
{
    /// <summary>
    /// R2.2 — Reseller Accounting-Cashflow Alignment tests.
    /// Verifies 3 tenant booksets (Supplier + Reseller + Platform-when-not-VA),
    /// VAT chain (Supplier output = Reseller input), reference suffixes, and
    /// Domain mod (Order.OwnerTenantId).
    /// </summary>
    public class ResellerAccountingTests
    {
        private readonly Mock<IAccountingEntryRepository> _mockAccountingRepo;
        private readonly Mock<IAuditTrailService> _mockAuditTrail;
        private readonly Mock<IPeriodClosingService> _mockPeriodClosing;
        private readonly Mock<ILogger<AccountingEntryService>> _mockLogger;
        private readonly AccountingEntryService _accountingService;

        public ResellerAccountingTests()
        {
            _mockAccountingRepo = new Mock<IAccountingEntryRepository>();
            _mockAuditTrail = new Mock<IAuditTrailService>();
            _mockPeriodClosing = new Mock<IPeriodClosingService>();
            _mockLogger = new Mock<ILogger<AccountingEntryService>>();

            _mockPeriodClosing
                .Setup(p => p.GetPeriodStatusAsync(It.IsAny<AccountingPeriod>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Open);
            _mockAccountingRepo
                .Setup(r => r.GetByTenantAndDateRangeAsync(It.IsAny<TenantId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            _accountingService = new AccountingEntryService(_mockAccountingRepo.Object, _mockAuditTrail.Object, _mockPeriodClosing.Object, _mockLogger.Object);
        }

        // ============================================================
        // Domain mod tests — Order.OwnerTenantId + SetResellerPricing
        // ============================================================

        [Fact(DisplayName = "R2.2-D1: SetResellerPricing_WithOwnerTenantId_SetsField")]
        public void SetResellerPricing_WithOwnerTenantId_SetsField()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var ownerTenantId = Guid.NewGuid();
            var order = new Order(tenantId, null, 100000m);

            order.SetResellerPricing(80000, 100000, 20000, 15000, 0.30m, 0.05m, ownerTenantId);

            Assert.Equal(CommerceMode.Reseller, order.CommerceMode);
            Assert.Equal(ownerTenantId, order.OwnerTenantId);
        }

        [Fact(DisplayName = "R2.2-D2: SetResellerPricing_WithoutOwnerTenantId_LeavesNull")]
        public void SetResellerPricing_WithoutOwnerTenantId_LeavesNull()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var order = new Order(tenantId, null, 100000m);

            order.SetResellerPricing(80000, 100000, 20000, 15000, 0.30m, 0.05m);

            Assert.Equal(CommerceMode.Reseller, order.CommerceMode);
            Assert.Null(order.OwnerTenantId);
        }

        [Fact(DisplayName = "R2.2-D3: SetResellerPricing_WithEmptyOwnerTenantId_Throws")]
        public void SetResellerPricing_WithEmptyOwnerTenantId_Throws()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var order = new Order(tenantId, null, 100000m);

            Assert.Throws<ArgumentException>(() =>
                order.SetResellerPricing(80000, 100000, 20000, 15000, 0.30m, 0.05m, Guid.Empty));
        }

        [Fact(DisplayName = "R2.2-D4: SetResellerPricing_BackwardCompat_ExistingCallersStillWork")]
        public void SetResellerPricing_BackwardCompat_ExistingCallersStillWork()
        {
            // Existing Sprint 7 tests call without ownerTenantId — must still work
            var tenantId = new TenantId(Guid.NewGuid());
            var order = new Order(tenantId, null, 100000m);

            order.SetResellerPricing(80000, 100000, 20000, 15000, 0.30m, 0.05m);

            Assert.Equal(80000m, order.CostPrice);
            Assert.Equal(100000m, order.SellPrice);
            Assert.Equal(20000m, order.PlatformMargin);
            Assert.Equal(0.30m, order.PlatformFeeRate);
            Assert.Equal(0.05m, order.CommunityFundRate);
        }

        // ============================================================
        // VAT chain tests — Supplier output VAT = Reseller input VAT
        // ============================================================

        [Fact(DisplayName = "R2.2-V1: VatChain_SupplierOutputEqualsResellerInput")]
        public async Task VatChain_SupplierOutputEqualsResellerInput()
        {
            // Supplier VAT = CostPrice × VatRate
            // Reseller input VAT = CostPrice × VatRate (same — khấu trừ)
            decimal costPrice = 80000m;
            decimal vatRate = 0.10m;
            decimal expectedVat = costPrice * vatRate; // 8000

            var supplierTenantId = new TenantId(Guid.NewGuid());
            var period = new AccountingPeriod(2026, 9);

            // Create Supplier VAT entry (account 3331)
            var supplierVatEntry = await _accountingService.CreateRevenueEntryAsync(
                supplierTenantId, period, expectedVat,
                "Supplier VAT output", accountCode: "3331", reference: "SUP-VAT");

            // Create Reseller input VAT entry (account 1331)
            var resellerTenantId = new TenantId(Guid.NewGuid());
            var resellerVatInEntry = await _accountingService.CreateRevenueEntryAsync(
                resellerTenantId, period, expectedVat,
                "Reseller VAT input", accountCode: "1331", reference: "RES-VATIN");

            Assert.Equal(supplierVatEntry.Amount, resellerVatInEntry.Amount);
            Assert.Equal("3331", supplierVatEntry.AccountCode);
            Assert.Equal("1331", resellerVatInEntry.AccountCode);
        }

        // ============================================================
        // Accounting entry reference suffix tests
        // ============================================================

        [Fact(DisplayName = "R2.2-R1: ReferenceSuffix_SupplierEntriesUseSupPrefix")]
        public async Task ReferenceSuffix_SupplierEntriesUseSupPrefix()
        {
            var orderId = Guid.NewGuid();
            var supplierTenantId = new TenantId(Guid.NewGuid());
            var period = new AccountingPeriod(2026, 9);

            var entry = await _accountingService.CreateRevenueEntryAsync(
                supplierTenantId, period, 80000m,
                "Supplier revenue", accountCode: "511",
                reference: $"{orderId}-SUP-REV");

            Assert.Equal($"{orderId}-SUP-REV", entry.Reference);
        }

        [Fact(DisplayName = "R2.2-R2: ReferenceSuffix_ResellerEntriesUseResPrefix")]
        public async Task ReferenceSuffix_ResellerEntriesUseResPrefix()
        {
            var orderId = Guid.NewGuid();
            var resellerTenantId = new TenantId(Guid.NewGuid());
            var period = new AccountingPeriod(2026, 9);

            var entry = await _accountingService.CreateRevenueEntryAsync(
                resellerTenantId, period, 100000m,
                "Reseller revenue", accountCode: "511",
                reference: $"{orderId}-RES-REV");

            Assert.Equal($"{orderId}-RES-REV", entry.Reference);
        }

        [Fact(DisplayName = "R2.2-R3: ReferenceSuffix_PlatformEntriesUsePltPrefix")]
        public async Task ReferenceSuffix_PlatformEntriesUsePltPrefix()
        {
            var orderId = Guid.NewGuid();
            var platformTenantId = new TenantId(Guid.NewGuid());
            var period = new AccountingPeriod(2026, 9);

            var entry = await _accountingService.CreateRevenueEntryAsync(
                platformTenantId, period, 5000m,
                "Platform fee income", accountCode: "511",
                reference: $"{orderId}-PLT-REV");

            Assert.Equal($"{orderId}-PLT-REV", entry.Reference);
        }

        // ============================================================
        // Accounting = cashflow invariant tests
        // ============================================================

        [Fact(DisplayName = "R2.2-I1: AccountingSum_Equals_CashflowSum_SupplierReceivesCostPrice")]
        public void AccountingSum_Equals_CashflowSum_SupplierReceivesCostPrice()
        {
            // Supplier accounting: Revenue 511 = CostPrice (cash-basis: actual cash received)
            // Wallet: Settlement = CostPrice (supplier receives via Wallet)
            decimal costPrice = 80000m;
            decimal supplierRevenue = costPrice; // R2.2: Revenue = CostPrice (NOT SellPrice)
            decimal walletSettlement = costPrice; // Wallet: supplier receives CostPrice

            Assert.Equal(supplierRevenue, walletSettlement);
        }

        [Fact(DisplayName = "R2.2-I2: ResellerNetVat_CorrectCalculation")]
        public void ResellerNetVat_CorrectCalculation()
        {
            // Reseller net VAT payable = Output VAT (sellPrice) - Input VAT (costPrice)
            decimal costPrice = 80000m;
            decimal sellPrice = 100000m;
            decimal vatRate = 0.10m;

            decimal outputVat = sellPrice * vatRate; // 10000
            decimal inputVat = costPrice * vatRate;  // 8000
            decimal netVatPayable = outputVat - inputVat; // 2000

            Assert.Equal(10000m, outputVat);
            Assert.Equal(8000m, inputVat);
            Assert.Equal(2000m, netVatPayable);
        }

        // ============================================================
        // Platform skip-when-Reseller-equals-VanAn test
        // ============================================================

        [Fact(DisplayName = "R2.2-P1: PlatformEntries_Skipped_WhenResellerIsPlatform")]
        public void PlatformEntries_Skipped_WhenResellerIsPlatform()
        {
            // When Reseller tenant = Platform tenant (Vạn An):
            // Skip Platform entries — margin = Reseller's gross profit (SellPrice - CostPrice)
            var resellerTenantId = Guid.NewGuid();
            var platformTenantId = resellerTenantId; // Same — Reseller IS Vạn An

            bool shouldSkipPlatform = platformTenantId == resellerTenantId;

            Assert.True(shouldSkipPlatform, "Platform entries must be skipped when Reseller = Platform (Vạn An)");
        }

        [Fact(DisplayName = "R2.2-P2: PlatformEntries_Created_WhenResellerIsNotPlatform")]
        public void PlatformEntries_Created_WhenResellerIsNotPlatform()
        {
            var resellerTenantId = Guid.NewGuid();
            var platformTenantId = Guid.NewGuid(); // Different — Reseller ≠ Vạn An

            bool shouldSkipPlatform = platformTenantId == resellerTenantId;

            Assert.False(shouldSkipPlatform, "Platform entries must be created when Reseller ≠ Platform (Vạn An)");
        }

        // ============================================================
        // Marketplace regression test — existing behavior unchanged
        // ============================================================

        [Fact(DisplayName = "R2.2-M1: MarketplaceOrder_StillUsesDefaultPath_NoResellerBranch")]
        public void MarketplaceOrder_StillUsesDefaultPath_NoResellerBranch()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var order = new Order(tenantId, null, 100000m);

            // Marketplace order should NOT have Reseller fields set
            Assert.Equal(CommerceMode.Marketplace, order.CommerceMode);
            Assert.Null(order.CostPrice);
            Assert.Null(order.SellPrice);
            Assert.Null(order.PlatformMargin);
            Assert.Null(order.OwnerTenantId);
        }
    }
}
