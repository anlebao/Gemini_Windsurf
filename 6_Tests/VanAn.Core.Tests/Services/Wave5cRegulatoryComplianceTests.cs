using Moq;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Orchestration;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// Wave 5c unit tests — 2026 Regulatory Compliance Fix.
    /// Verifies 2026 thresholds (1B/3B/50B), TNCN formulas per Nhóm 2/3/4, GTGT Nhóm 1 exemption.
    /// Per Luật Thuế GTGT/TNCN sửa đổi 2025 + ND 117/2025/NĐ-CP + Nghị quyết 198/2025/QH15.
    /// Covers W5c SC1-SC8 acceptance criteria.
    /// </summary>
    public class Wave5cRegulatoryComplianceTests
    {
        // ── Constants per 2026 law ──
        private const decimal OneBillion = 1_000_000_000m;
        private const decimal ThreeBillion = 3_000_000_000m;
        private const decimal FiftyBillion = 50_000_000_000m;

        // ──────────────────────────────────────────────────────────────────
        // SC1 + SC8-Test1: 2026 Thresholds (1B / 3B / 50B)
        // ──────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(0, HKDRevenueGroup.Group1)]                    // Zero revenue → Group1
        [InlineData(500_000_000, HKDRevenueGroup.Group1)]          // 500M → Group1 (≤1B)
        [InlineData(1_000_000_000, HKDRevenueGroup.Group1)]        // Exactly 1B → Group1 (≤1B, boundary)
        [InlineData(1_000_000_001, HKDRevenueGroup.Group2)]        // 1B + 1₫ → Group2 (>1B)
        [InlineData(2_000_000_000, HKDRevenueGroup.Group2)]        // 2B → Group2 (1B-3B)
        [InlineData(3_000_000_000, HKDRevenueGroup.Group2)]        // Exactly 3B → Group2 (≤3B, boundary)
        [InlineData(3_000_000_001, HKDRevenueGroup.Group3)]        // 3B + 1₫ → Group3 (>3B)
        [InlineData(25_000_000_000, HKDRevenueGroup.Group3)]       // 25B → Group3 (3B-50B)
        [InlineData(50_000_000_000, HKDRevenueGroup.Group3)]       // Exactly 50B → Group3 (≤50B, boundary)
        [InlineData(50_000_000_001, HKDRevenueGroup.Group4)]       // 50B + 1₫ → Group4 (>50B)
        [InlineData(100_000_000_000, HKDRevenueGroup.Group4)]      // 100B → Group4 (>50B)
        public void RevenueGroup_ShouldUse2026Thresholds_1B_3B_50B(decimal totalRevenue, HKDRevenueGroup expectedGroup)
        {
            // Act
            HKDRevenueGroup group = HKDRevenueClassification.CalculateGroup(totalRevenue);

            // Assert
            group.Should().Be(expectedGroup,
                "2026 regulatory thresholds are 1B/3B/50B per Luật GTGT/TNCN sửa đổi 2025");
        }

        // ──────────────────────────────────────────────────────────────────
        // SC3 + SC8-Test2: TNCN Nhóm 2 — (Doanh thu - 1B) × industryRate
        // ──────────────────────────────────────────────────────────────────

        [Fact]
        public void TNCN_Group2_ShouldSubtract1B_BeforeApplyingRate()
        {
            // Arrange: revenue 2B, Distribution sector (PIT rate 0.5% = 0.005)
            // Per task card example: TNCN = (2B - 1B) × 0.5% = 5,000,000₫
            decimal totalRevenue = 2_000_000_000m;
            decimal totalExpense = 0m;             // Nhóm 2 uses revenue-based formula, not profit
            decimal industryPitRate = 0.005m;      // 0.5% (Distribution per ND 117/2025)
            decimal expectedPit = 5_000_000m;      // (2B - 1B) × 0.5% = 5M

            // Act
            decimal pit = HKDRevenueClassification.CalculateTNCN(
                HKDRevenueGroup.Group2, totalRevenue, totalExpense, industryPitRate);

            // Assert
            pit.Should().Be(expectedPit,
                "Nhóm 2 TNCN = (Doanh thu - 1 tỷ) × industryRate, NOT Doanh thu × rate. " +
                "Old (wrong) formula would give 2B × 0.5% = 10M.");
        }

        [Fact]
        public void TNCN_Group2_WithRevenueUnder1B_ShouldBeZero()
        {
            // Arrange: revenue 800M (actually Group1, but verify Group2 formula with revenue < 1B)
            decimal totalRevenue = 800_000_000m;
            decimal industryPitRate = 0.005m;

            // Act
            decimal pit = HKDRevenueClassification.CalculateTNCN(
                HKDRevenueGroup.Group2, totalRevenue, 0m, industryPitRate);

            // Assert: Max(0, 800M - 1B) = Max(0, -200M) = 0
            pit.Should().Be(0m, "TNCN Nhóm 2 uses Max(0, Revenue - 1B) — negative result clamped to 0");
        }

        // ──────────────────────────────────────────────────────────────────
        // SC4 + SC8-Test3: TNCN Nhóm 3 — (Doanh thu - chi phí) × 17%
        // ──────────────────────────────────────────────────────────────────

        [Fact]
        public void TNCN_Group3_ShouldCalculateOnProfit_17Percent()
        {
            // Arrange: revenue 5B, chi phí 3B
            // Per task card: TNCN = (5B - 3B) × 17% = 340,000,000₫
            decimal totalRevenue = 5_000_000_000m;
            decimal totalExpense = 3_000_000_000m;
            decimal industryRate = 0m;             // Not used for Nhóm 3 (profit-based)
            decimal expectedPit = 340_000_000m;    // (5B - 3B) × 17% = 340M

            // Act
            decimal pit = HKDRevenueClassification.CalculateTNCN(
                HKDRevenueGroup.Group3, totalRevenue, totalExpense, industryRate);

            // Assert
            pit.Should().Be(expectedPit,
                "Nhóm 3 TNCN = (Doanh thu - chi phí) × 17% (bắt buộc theo lợi nhuận)");
        }

        // ──────────────────────────────────────────────────────────────────
        // SC5 + SC8-Test4: TNCN Nhóm 4 — (Doanh thu - chi phí) × 20%
        // ──────────────────────────────────────────────────────────────────

        [Fact]
        public void TNCN_Group4_ShouldCalculateOnProfit_20Percent()
        {
            // Arrange: revenue 60B, chi phí 40B
            // Per task card: TNCN = (60B - 40B) × 20% = 4,000,000,000₫
            decimal totalRevenue = 60_000_000_000m;
            decimal totalExpense = 40_000_000_000m;
            decimal industryRate = 0m;             // Not used for Nhóm 4 (profit-based)
            decimal expectedPit = 4_000_000_000m;  // (60B - 40B) × 20% = 4B

            // Act
            decimal pit = HKDRevenueClassification.CalculateTNCN(
                HKDRevenueGroup.Group4, totalRevenue, totalExpense, industryRate);

            // Assert
            pit.Should().Be(expectedPit,
                "Nhóm 4 TNCN = (Doanh thu - chi phí) × 20% (bắt buộc theo lợi nhuận)");
        }

        // ──────────────────────────────────────────────────────────────────
        // SC6 + SC8-Test5: GTGT Nhóm 1 — 0 (exemption, revenue ≤ 1B)
        // ──────────────────────────────────────────────────────────────────

        [Fact]
        public void GTGT_Group1_ShouldBeZero_WhenRevenueUnder1B()
        {
            // Arrange: revenue 800M (Nhóm 1), Distribution sector (GTGT rate 1% = 0.01)
            decimal totalRevenue = 800_000_000m;
            decimal industryVatRate = 0.01m;       // 1% (Distribution per ND 117/2025)

            // Act
            decimal vat = HKDRevenueClassification.CalculateGTGT(
                HKDRevenueGroup.Group1, totalRevenue, industryVatRate);

            // Assert
            vat.Should().Be(0m,
                "Nhóm 1 (revenue ≤ 1B) is GTGT-exempt per 2026 law. " +
                "Old formula would give 800M × 1% = 8M (wrong).");
        }

        [Fact]
        public void GTGT_Group2_ShouldBeRevenueTimesIndustryRate()
        {
            // Arrange: revenue 2B (Nhóm 2), Distribution sector (GTGT rate 1%)
            decimal totalRevenue = 2_000_000_000m;
            decimal industryVatRate = 0.01m;
            decimal expectedVat = 20_000_000m;     // 2B × 1% = 20M

            // Act
            decimal vat = HKDRevenueClassification.CalculateGTGT(
                HKDRevenueGroup.Group2, totalRevenue, industryVatRate);

            // Assert
            vat.Should().Be(expectedVat,
                "Nhóm 2/3/4 GTGT = Doanh thu × industryVatRate (per ND 117/2025)");
        }

        // ──────────────────────────────────────────────────────────────────
        // SC1: HKDRevenueClassificationService thresholds (Service layer)
        // ──────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Service_ShouldUse2026Thresholds_ForWarningMessages()
        {
            // Arrange: revenue 950M (90% of 1B → should trigger Nhóm 2 warning)
            var mockAccountingService = new Mock<IAccountingService>();
            var entries = new List<AccountingEntryDto>
            {
                new() { EntryType = AccountingEntryType.Revenue, Amount = 950_000_000m }
            };
            mockAccountingService
                .Setup(s => s.GetEntriesByTenantAndPeriodAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>()))
                .ReturnsAsync(entries);

            var service = new HKDRevenueClassificationService(mockAccountingService.Object);
            var tenantId = new TenantId(Guid.NewGuid());
            var period = new AccountingPeriod(2026, 7);

            // Act
            List<string> warnings = await service.GetThresholdWarningsAsync(tenantId, period);

            // Assert: 950M > 1B × 0.90 = 900M → should warn about approaching Nhóm 2 (1B)
            warnings.Should().NotBeEmpty();
            warnings.Should().Contain(w => w.Contains("Nhóm 2") && w.Contains("1 tỷ"),
                "2026 threshold for Nhóm 2 is 1B (not 500M as pre-2026)");
        }

        [Fact]
        public async Task Service_ShouldNotWarn_WhenRevenueBelow90PercentOf1B()
        {
            // Arrange: revenue 800M (< 900M = 90% of 1B → no warning)
            var mockAccountingService = new Mock<IAccountingService>();
            var entries = new List<AccountingEntryDto>
            {
                new() { EntryType = AccountingEntryType.Revenue, Amount = 800_000_000m }
            };
            mockAccountingService
                .Setup(s => s.GetEntriesByTenantAndPeriodAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>()))
                .ReturnsAsync(entries);

            var service = new HKDRevenueClassificationService(mockAccountingService.Object);
            var tenantId = new TenantId(Guid.NewGuid());
            var period = new AccountingPeriod(2026, 7);

            // Act
            List<string> warnings = await service.GetThresholdWarningsAsync(tenantId, period);

            // Assert: 800M < 900M (90% of 1B) → no threshold warning
            warnings.Should().BeEmpty();
        }

        // ──────────────────────────────────────────────────────────────────
        // TNCN Nhóm 1 — should be 0 (không chịu thuế)
        // ──────────────────────────────────────────────────────────────────

        [Fact]
        public void TNCN_Group1_ShouldBeZero()
        {
            // Arrange: revenue 800M (Nhóm 1)
            decimal totalRevenue = 800_000_000m;
            decimal industryRate = 0.005m;

            // Act
            decimal pit = HKDRevenueClassification.CalculateTNCN(
                HKDRevenueGroup.Group1, totalRevenue, 0m, industryRate);

            // Assert
            pit.Should().Be(0m, "Nhóm 1 (revenue ≤ 1B) does not pay TNCN per 2026 law");
        }
    }
}
