using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Infrastructure.Seed;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Journal;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;
using DomainAccountType = VanAn.Shared.Domain.AccountType;
using Xunit;

namespace VanAn.Core.Tests.Accounting
{
    /// <summary>
    /// SRS v1.2 W2 (B+C1 — approved 2026-10-31): account map resolver (T-parity, precedence),
    /// tenant account chart (FR-COA-005/006), voucher numbering (FR-VCH-004), VAT JE.
    /// </summary>
    public class AccountingEngineW2Tests
    {
        // ============ T1: PARITY MATRIX — builtin = hardcode cũ ============

        [Theory]
        // Universal events — parity ResolveAccounts cũ (cash side 111 / {cash})
        [InlineData("manual-receipt", TenantType.Enterprise_SME, "511", "111", "511")]
        [InlineData("manual-receipt", TenantType.HTX, "512", "111", "512")]
        [InlineData("manual-expense", TenantType.Enterprise_SME, "642", "642", "111")]
        [InlineData("deposit-offset", TenantType.Enterprise_SME, "511", "3387", "511")]
        [InlineData("order-revenue", TenantType.Enterprise_SME, "511", "111", "511")]
        [InlineData("order-revenue", TenantType.HTX, "512", "111", "512")]
        [InlineData("order-vat", TenantType.Enterprise_SME, "3331", "111", "3331")] // MỚI — GL missing VAT fix
        [InlineData("order-cogs", TenantType.Enterprise_SME, "632", "632", "111")]
        [InlineData("order-cogs", TenantType.HTX, "611", "611", "111")]
        // Accrual events — chỉ DN/HTX (credit 511 DN, 512 HTX)
        [InlineData("receivable", TenantType.Enterprise_SME, "131", "131", "511")]
        [InlineData("receivable", TenantType.Enterprise_Large, "131", "131", "511")]
        [InlineData("receivable", TenantType.HTX, "131", "131", "512")] // T5: HTX → 512
        [InlineData("receivable-payment", TenantType.Enterprise_SME, "131", "111", "131")]
        [InlineData("payable-payment", TenantType.Enterprise_SME, "331", "331", "111")]
        public async Task T1_BuiltinParity(string eventCode, TenantType tenantType, string hint, string expectedDebit, string expectedCredit)
        {
            using TestContextScope scope = VanAnDbContextTestFactory.Create();
            var resolver = CreateResolver(scope.Context, tenantType);

            AccountMapResult? result = await resolver.ResolveAsync(eventCode, new TenantId(Guid.NewGuid()), hint);

            result.Should().NotBeNull();
            result!.Debit.Should().Be(expectedDebit);
            result.Credit.Should().Be(expectedCredit);
            result.MappingVersion.Should().Be("builtin");
        }

        [Theory]
        // HKD/DNSN: accrual events → null (cash-basis) · payable: no-rule mọi regime
        [InlineData("receivable", TenantType.HKD)]
        [InlineData("receivable", TenantType.Enterprise_SuperSmall)]
        [InlineData("receivable-payment", TenantType.HKD)]
        [InlineData("payable", TenantType.Enterprise_SME)]
        [InlineData("payable", TenantType.HTX)]
        [InlineData("unknown-event", TenantType.Enterprise_SME)]
        public async Task T6_NoRule_ReturnsNull(string eventCode, TenantType tenantType)
        {
            using TestContextScope scope = VanAnDbContextTestFactory.Create();
            var resolver = CreateResolver(scope.Context, tenantType);

            AccountMapResult? result = await resolver.ResolveAsync(eventCode, new TenantId(Guid.NewGuid()), "131");

            result.Should().BeNull(); // Skipped("no-rule") ở bridge — không đoán
        }

        // ============ T2: PRECEDENCE — tenant > regime > builtin ============

        [Fact]
        public async Task T2_TenantRule_WinsOverRegimeAndBuiltin()
        {
            using TestContextScope scope = VanAnDbContextTestFactory.Create();
            TenantId tenant = new(Guid.NewGuid());
            var resolver = CreateResolver(scope.Context, TenantType.Enterprise_SME);

            // Regime rule (TT133): order-revenue → (111, 512)
            scope.Context.AccountMappingRules.Add(new AccountMappingRuleEntity(
                "order-revenue", "111", "512", tenantId: null, regime: "TT133"));
            // Tenant rule: order-revenue → (113, 599)
            scope.Context.AccountMappingRules.Add(new AccountMappingRuleEntity(
                "order-revenue", "113", "599", tenantId: tenant.Value));
            await scope.Context.SaveChangesAsync();

            AccountMapResult? result = await resolver.ResolveAsync("order-revenue", tenant, "511");

            result.Should().NotBeNull();
            result!.Debit.Should().Be("113"); // tenant rule thắng
            result.Credit.Should().Be("599");
            result.MappingVersion.Should().Be("v1");
        }

        [Fact]
        public async Task T2_RegimeRule_WinsOverBuiltin_WhenNoTenantRule()
        {
            using TestContextScope scope = VanAnDbContextTestFactory.Create();
            var resolver = CreateResolver(scope.Context, TenantType.Enterprise_SME);

            scope.Context.AccountMappingRules.Add(new AccountMappingRuleEntity(
                "order-vat", "112", "3338", tenantId: null, regime: "TT133", version: 3));
            await scope.Context.SaveChangesAsync();

            AccountMapResult? result = await resolver.ResolveAsync("order-vat", new TenantId(Guid.NewGuid()), "3331");

            result.Should().NotBeNull();
            result!.Debit.Should().Be("112"); // regime rule thắng builtin (111,3331)
            result.Credit.Should().Be("3338");
            result.MappingVersion.Should().Be("v3");
        }

        [Fact]
        public async Task T2_CashPlaceholder_UsesCashAccountCode()
        {
            using TestContextScope scope = VanAnDbContextTestFactory.Create();
            var resolver = CreateResolver(scope.Context, TenantType.Enterprise_SME);

            // cashHint "112" → {cash} = 112 (Phase D nền)
            AccountMapResult? result = await resolver.ResolveAsync(
                "manual-receipt", new TenantId(Guid.NewGuid()), "511", cashHint: "112");

            result!.Debit.Should().Be("112");
            result.Credit.Should().Be("511");
        }

        // ============ T3: VAT JE — flag ON → bridge tạo JE (111,3331) ============

        [Fact]
        public async Task T3_VatEntry_FlagOn_CreatesVatJournalEntry()
        {
            TenantId tenant = new(Guid.NewGuid());
            var hkdRepo = new Mock<IHKDBookRepository>();
            var flags = new Mock<IShopFeatureSettingsService>();
            flags.Setup(f => f.IsEnabledAsync(tenant.Value, ManualEntryJournalBridge.AccountMappingFlag, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var resolver = new Mock<IAccountMapResolver>();
            resolver.Setup(r => r.ResolveAsync(AccountMapEventCodes.OrderVat, tenant, "3331", "111", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AccountMapResult("111", "3331", "v1"));

            var bridge = new ManualEntryJournalBridge(
                hkdRepo.Object, NullLogger<ManualEntryJournalBridge>.Instance,
                featureSettings: flags.Object, accountMapResolver: resolver.Object);

            CoreAccountingEntry vatEntry = CoreAccountingEntry.CreateRevenue(
                tenant, new AccountingPeriod(2026, 10), new Money(70_000m), "Thuế GTGT đầu ra",
                accountCode: "3331", reference: Guid.NewGuid().ToString(),
                postingPurpose: AccountingPostingPurposes.Vat, sourceEventType: "Order");

            bool created = await bridge.CreateForAccountingEntryAsync(vatEntry);

            created.Should().BeTrue();
            hkdRepo.Verify(r => r.AddToBookAsync(
                It.Is<JournalEntry>(je =>
                    je.Lines.Count == 2 &&
                    je.Lines.First().AccountNumber == "111" && je.Lines.First().DebitAmount == 70_000m &&
                    je.Lines.ElementAt(1).AccountNumber == "3331" && je.Lines.ElementAt(1).CreditAmount == 70_000m &&
                    je.MappingVersion == "v1" &&
                    je.PostingPurpose == AccountingPostingPurposes.Vat),
                AccountingBookType.S2b_HKD, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task T3_VatEntry_FlagOff_LegacyPath_NotJournalable()
        {
            TenantId tenant = new(Guid.NewGuid());
            var hkdRepo = new Mock<IHKDBookRepository>();
            var flags = new Mock<IShopFeatureSettingsService>();
            flags.Setup(f => f.IsEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var bridge = new ManualEntryJournalBridge(
                hkdRepo.Object, NullLogger<ManualEntryJournalBridge>.Instance,
                featureSettings: flags.Object);

            CoreAccountingEntry vatEntry = CoreAccountingEntry.CreateRevenue(
                tenant, new AccountingPeriod(2026, 10), new Money(70_000m), "Thuế GTGT",
                accountCode: "3331", postingPurpose: AccountingPostingPurposes.Vat);

            bool created = await bridge.CreateForAccountingEntryAsync(vatEntry);

            created.Should().BeFalse(); // legacy: 3331 không journalable
            hkdRepo.Verify(r => r.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ============ T4: TENANT ACCOUNT CHART (FR-COA-005/006) ============

        [Fact]
        public async Task T4_CustomAccount_RequiresExistingParent()
        {
            using TestContextScope scope = VanAnDbContextTestFactory.Create();
            await AccountChartSeeder.SeedAsync(scope.Context, NullLogger.Instance);
            var svc = new AccountChartService(scope.Context, NullLogger<AccountChartService>.Instance);
            TenantId tenant = new(Guid.NewGuid());

            // Parent "9999" không tồn tại → reject
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpsertTenantAccountAsync(
                tenant, "5111", "DT chi tiết", TenantAccountChartSources.Custom,
                AccountingStandard.TT133_2016, DomainAccountType.Revenue, parentCode: "9999"));

            // Parent "511" có trong global → OK
            TenantAccountChartEntity row = await svc.UpsertTenantAccountAsync(
                tenant, "5111", "DT chi tiết", TenantAccountChartSources.Custom,
                AccountingStandard.TT133_2016, DomainAccountType.Revenue, parentCode: "511");
            row.IsActive.Should().BeTrue();

            // Resolve: tenant custom có trong tenant chart
            AccountChartEntry? resolved = await svc.GetTenantAccountAsync(tenant, "5111", AccountingStandard.TT133_2016);
            resolved.Should().NotBeNull();
            resolved!.AccountName.Should().Be("DT chi tiết");
        }

        [Fact]
        public async Task T4_Override_RequiresGlobalAccount_And_Wins()
        {
            using TestContextScope scope = VanAnDbContextTestFactory.Create();
            await AccountChartSeeder.SeedAsync(scope.Context, NullLogger.Instance);
            var svc = new AccountChartService(scope.Context, NullLogger<AccountChartService>.Instance);
            TenantId tenant = new(Guid.NewGuid());

            // Override TK không có trong global → reject
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpsertTenantAccountAsync(
                tenant, "9999", "X", TenantAccountChartSources.Override, AccountingStandard.TT133_2016));

            // Override 511 → đổi tên hiển thị riêng tenant
            await svc.UpsertTenantAccountAsync(tenant, "511", "DT bán hàng (tên riêng)",
                TenantAccountChartSources.Override, AccountingStandard.TT133_2016);

            AccountChartEntry? resolved = await svc.GetTenantAccountAsync(tenant, "511", AccountingStandard.TT133_2016);
            resolved!.AccountName.Should().Be("DT bán hàng (tên riêng)"); // tenant thắng global
        }

        [Fact]
        public async Task T4_DeactivateAllowed_DeleteRejected()
        {
            using TestContextScope scope = VanAnDbContextTestFactory.Create();
            await AccountChartSeeder.SeedAsync(scope.Context, NullLogger.Instance);
            var svc = new AccountChartService(scope.Context, NullLogger<AccountChartService>.Instance);
            TenantId tenant = new(Guid.NewGuid());

            await svc.UpsertTenantAccountAsync(tenant, "5111", "DT chi tiết",
                TenantAccountChartSources.Custom, AccountingStandard.TT133_2016,
                DomainAccountType.Revenue, parentCode: "511");

            // Deactivate TK custom → biến mất khỏi tenant chart
            await svc.DeactivateTenantAccountAsync(tenant, "5111");
            AccountChartEntry? resolved = await svc.GetTenantAccountAsync(tenant, "5111", AccountingStandard.TT133_2016);
            resolved.Should().BeNull();

            // FR-COA-005: xóa LUÔN bị chặn
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteTenantAccountAsync(tenant, "5111"));
        }

        // ============ T1-seq: VOUCHER NUMBERING (FR-VCH-004) ============

        [Fact]
        public async Task VoucherNumber_Sequential_PerTypeAndYear()
        {
            using TestContextScope scope = VanAnDbContextTestFactory.Create();
            var svc = new VoucherNumberService(scope.Context, NullLogger<VoucherNumberService>.Instance);
            TenantId tenant = new(Guid.NewGuid());

            string v1 = await svc.NextNumberAsync(tenant, VoucherNumberService.TypeReceipt, 2026);
            string v2 = await svc.NextNumberAsync(tenant, VoucherNumberService.TypeReceipt, 2026);
            string pc = await svc.NextNumberAsync(tenant, VoucherNumberService.TypePayment, 2026);
            string nextYear = await svc.NextNumberAsync(tenant, VoucherNumberService.TypeReceipt, 2027);

            v1.Should().Be("PT-2026-00001");
            v2.Should().Be("PT-2026-00002"); // tuần tự
            pc.Should().Be("PC-2026-00001"); // per-type
            nextYear.Should().Be("PT-2027-00001"); // năm mới reset
        }

        // ============ HELPERS ============

        private static AccountMapResolver CreateResolver(VanAnDbContext db, TenantType tenantType)
        {
            var flags = new Mock<IVasFeatureFlagService>();
            flags.Setup(f => f.GetTenantTypeAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tenantType);
            return new AccountMapResolver(db, NullLogger<AccountMapResolver>.Instance, flags.Object);
        }
    }
}
