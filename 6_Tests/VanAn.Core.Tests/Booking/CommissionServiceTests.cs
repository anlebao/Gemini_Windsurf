using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Booking;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;
using BookingEntity = VanAn.Shared.Domain.Booking;
using StaffServiceEntity = VanAn.Shared.Domain.StaffService;
using NotFoundException = VanAn.CoreHub.Services.NotFoundException;
using ValidationException = VanAn.CoreHub.Services.ValidationException;

namespace VanAn.Core.Tests.BookingScheduling
{
    /// <summary>
    /// ICommissionService — qualification §17.1 (COMPLETED + payment qualified + attribution valid),
    /// ledger snapshot immutable §17.5, reversal §26.6 (AC-Q03-Q05), payout → WalletTransaction, tenant isolation.
    /// </summary>
    public class CommissionServiceTests
    {
        private static CommissionService BuildCommissionService(VanAnDbContext ctx)
            => new(ctx, new TaxWithholdingPolicyAdapter(), new FakeWalletService(ctx), NullLogger<CommissionService>.Instance);

        private static BookingService BuildBookingService(VanAnDbContext ctx)
            => new(ctx, new AvailabilityService(ctx, NullLogger<AvailabilityService>.Instance), NullLogger<BookingService>.Instance);

        /// <summary>Seed tenant + salesman + QR + resolve → trả attribution session id (qualified).</summary>
        private static async Task<Guid> SeedAttributionAsync(VanAnDbContext ctx, string token = "tok-c1", string anon = "anon-c1")
        {
            await BookingTestData.SeedActiveTenantAsync(ctx, BookingTestData.TenantId);
            Guid salesmanId = await BookingTestData.SeedSalesmanAsync(ctx, BookingTestData.TenantId);
            var qrSvc = new QRAttributionService(ctx, NullLogger<QRAttributionService>.Instance);
            var qr = await qrSvc.CreateQrChannelAsync(BookingTestData.TenantId, token, salesmanId);
            var resolved = await qrSvc.ResolveQrAsync(token, anon);
            Assert.Equal(salesmanId, resolved.SalesmanId);
            return resolved.AttributionSessionId;
        }

        /// <summary>Tạo booking hoàn tất đầy đủ (create → confirm → assign → check-in → start → complete).</summary>
        private static async Task<BookingEntity> CompleteBookingAsync(VanAnDbContext ctx, Guid attributionId, decimal actualTotal = 300_000m,
            Guid? staffId = null, Guid? offeringId = null)
        {
            var bookingSvc = BuildBookingService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            if (staffId is null || offeringId is null)
                (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var booking = await bookingSvc.CreateBookingAsync(BookingTestData.TenantId,
                new CreateBookingCommand(OfferingId: offeringId.Value, StartAt: BookingTestData.At(14, 0), StaffId: null,
                    CustomerId: null, "device-c1", "Note", [], attributionId),
                "key-c1");
            await bookingSvc.ConfirmAsync(BookingTestData.TenantId, booking.Id);
            await bookingSvc.AssignStaffAsync(BookingTestData.TenantId, booking.Id, staffId.Value);
            await bookingSvc.CheckInAsync(BookingTestData.TenantId, booking.Id);
            await bookingSvc.StartServiceAsync(BookingTestData.TenantId, booking.Id);
            await bookingSvc.CompleteAsync(BookingTestData.TenantId, booking.Id, actualTotal);
            return booking;
        }

        private static async Task SeedCommissionRuleAsync(VanAnDbContext ctx, Guid? offeringId = null, Guid? salesmanId = null,
            CommissionType type = CommissionType.Percentage, decimal value = 10m)
        {
            ctx.CommissionRules.Add(new CommissionRule(BookingTestData.TenantId, type, value, offeringId, salesmanId));
            await ctx.SaveChangesAsync();
        }

        [Fact]
        public async Task Finalize_Completed_Paid_AttributionValid_CreatesEarnedEntry()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            Guid attributionId = await SeedAttributionAsync(ctx);
            await SeedCommissionRuleAsync(ctx, value: 10m);   // 10% global
            var booking = await CompleteBookingAsync(ctx, attributionId);

            var commissionSvc = BuildCommissionService(ctx);
            var entry = await commissionSvc.FinalizeCommissionForBookingAsync(BookingTestData.TenantId, booking.Id);

            Assert.NotNull(entry);
            Assert.Equal(CommissionLedgerState.Earned, entry!.State);
            Assert.Equal(CommissionSourceType.Booking, entry.SourceType);
            Assert.Equal(booking.Id, entry.BookingId);
            Assert.Equal(300_000m, entry.BaseAmount);
            Assert.Equal(30_000m, entry.GrossCommissionAmount);
            // Tax snapshot NĐ 253/2026: dưới 5tr → 0 khấu trừ.
            Assert.Equal(0m, entry.TaxWithheldAmount);
            Assert.Equal(30_000m, entry.NetCommissionAmount);
            Assert.Equal(TaxWithholdingPolicyAdapter.VersionNd253_2026, entry.TaxRuleVersion);
            Assert.NotNull(entry.FinalizedAt);

            // Rule snapshot JSON có ruleId.
            using var doc = JsonDocument.Parse(entry.RuleSnapshotJson);
            Assert.True(doc.RootElement.TryGetProperty("Id", out _));
        }

        [Fact]
        public async Task Finalize_NoAttribution_ReturnsNull()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            await SeedCommissionRuleAsync(ctx);
            var booking = await CompleteBookingAsync(ctx, attributionId: Guid.NewGuid());   // attribution không tồn tại

            var entry = await BuildCommissionService(ctx).FinalizeCommissionForBookingAsync(BookingTestData.TenantId, booking.Id);

            Assert.Null(entry);
            Assert.Equal(0, await ctx.CommissionLedgerEntries.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task Finalize_NotCompleted_ReturnsNull()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            Guid attributionId = await SeedAttributionAsync(ctx);
            await SeedCommissionRuleAsync(ctx);
            var bookingSvc = BuildBookingService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (_, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);
            var booking = await bookingSvc.CreateBookingAsync(BookingTestData.TenantId,
                new CreateBookingCommand(OfferingId: offeringId, StartAt: BookingTestData.At(14, 0), StaffId: null, CustomerId: null, CustomerDeviceId: "device-x", CustomerNote: null, AddOns: [], AttributionId: attributionId),
                "key-nc");

            var entry = await BuildCommissionService(ctx).FinalizeCommissionForBookingAsync(BookingTestData.TenantId, booking.Id);

            Assert.Null(entry);
        }

        [Fact]
        public async Task Finalize_DepositPending_PaymentNotQualified_ReturnsNull()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            Guid attributionId = await SeedAttributionAsync(ctx);
            await SeedCommissionRuleAsync(ctx);
            // Deposit Fixed → booking PaymentStatus=Pending (chưa paid) → payment không qualified.
            await BookingTestData.EnsureConfigWithDepositAsync(ctx, BookingTestData.TenantId, DepositPolicy.Fixed, fixedAmount: 100_000m);
            var bookingSvc = BuildBookingService(ctx);
            var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);
            var booking = await bookingSvc.CreateBookingAsync(BookingTestData.TenantId,
                new CreateBookingCommand(OfferingId: offeringId, StartAt: BookingTestData.At(14, 0), StaffId: null, CustomerId: null, CustomerDeviceId: "device-x", CustomerNote: null, AddOns: [], AttributionId: attributionId),
                "key-dep");
            await bookingSvc.ConfirmAsync(BookingTestData.TenantId, booking.Id);
            await bookingSvc.AssignStaffAsync(BookingTestData.TenantId, booking.Id, staffId);
            await bookingSvc.CheckInAsync(BookingTestData.TenantId, booking.Id);
            await bookingSvc.StartServiceAsync(BookingTestData.TenantId, booking.Id);
            await bookingSvc.CompleteAsync(BookingTestData.TenantId, booking.Id, 300_000m);
            Assert.Equal(PaymentStatus.Pending, (await bookingSvc.GetBookingAsync(BookingTestData.TenantId, booking.Id))!.PaymentStatus);

            var entry = await BuildCommissionService(ctx).FinalizeCommissionForBookingAsync(BookingTestData.TenantId, booking.Id);

            Assert.Null(entry);
        }

        [Fact]
        public async Task Finalize_NoActiveRule_ReturnsNull()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            Guid attributionId = await SeedAttributionAsync(ctx);
            // KHÔNG seed rule.
            var booking = await CompleteBookingAsync(ctx, attributionId);

            var entry = await BuildCommissionService(ctx).FinalizeCommissionForBookingAsync(BookingTestData.TenantId, booking.Id);

            Assert.Null(entry);
        }

        [Fact]
        public async Task Finalize_Idempotent_NoDoubleCreate()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            Guid attributionId = await SeedAttributionAsync(ctx);
            await SeedCommissionRuleAsync(ctx);
            var booking = await CompleteBookingAsync(ctx, attributionId);
            var commissionSvc = BuildCommissionService(ctx);

            var first = await commissionSvc.FinalizeCommissionForBookingAsync(BookingTestData.TenantId, booking.Id);
            var second = await commissionSvc.FinalizeCommissionForBookingAsync(BookingTestData.TenantId, booking.Id);

            Assert.Equal(first!.Id, second!.Id);
            Assert.Equal(1, await ctx.CommissionLedgerEntries.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task RuleMatching_MostSpecificWins()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            Guid attributionId = await SeedAttributionAsync(ctx);
            var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);
            await SeedCommissionRuleAsync(ctx, offeringId: null, salesmanId: null, type: CommissionType.FixedAmount, value: 10_000m);          // global
            Guid salesmanId = (await ctx.AttributionSessions.IgnoreQueryFilters().FirstAsync(s => s.Id == attributionId)).SalesmanId!.Value;
            await SeedCommissionRuleAsync(ctx, offeringId: offeringId, salesmanId: salesmanId, type: CommissionType.Percentage, value: 20m);  // specific

            var booking = await CompleteBookingAsync(ctx, attributionId, staffId: staffId, offeringId: offeringId);
            var entry = await BuildCommissionService(ctx).FinalizeCommissionForBookingAsync(BookingTestData.TenantId, booking.Id);

            Assert.Equal(60_000m, entry!.GrossCommissionAmount);   // 20% × 300k — specific rule thắng
        }

        [Fact]
        public async Task Reverse_EarnedEntry_CreatesReversalAndMarksOriginalReversed()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            Guid attributionId = await SeedAttributionAsync(ctx);
            await SeedCommissionRuleAsync(ctx);
            var booking = await CompleteBookingAsync(ctx, attributionId);
            var commissionSvc = BuildCommissionService(ctx);
            var entry = await commissionSvc.FinalizeCommissionForBookingAsync(BookingTestData.TenantId, booking.Id);

            var reversal = await commissionSvc.ReverseCommissionAsync(BookingTestData.TenantId, entry!.Id, "refund");

            Assert.Equal(CommissionLedgerState.Reversed, reversal.State);
            Assert.Equal(entry.Id, reversal.RelatedEntryId);
            Assert.Equal(entry.GrossCommissionAmount, reversal.GrossCommissionAmount);
            var originalReloaded = await ctx.CommissionLedgerEntries.IgnoreQueryFilters().FirstAsync(e => e.Id == entry.Id);
            Assert.Equal(CommissionLedgerState.Reversed, originalReloaded.State);
            Assert.Equal(2, await ctx.CommissionLedgerEntries.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task Reverse_Idempotent_ReturnsExistingReversal()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            Guid attributionId = await SeedAttributionAsync(ctx);
            await SeedCommissionRuleAsync(ctx);
            var booking = await CompleteBookingAsync(ctx, attributionId);
            var commissionSvc = BuildCommissionService(ctx);
            var entry = await commissionSvc.FinalizeCommissionForBookingAsync(BookingTestData.TenantId, booking.Id);

            var first = await commissionSvc.ReverseCommissionAsync(BookingTestData.TenantId, entry!.Id);
            var second = await commissionSvc.ReverseCommissionAsync(BookingTestData.TenantId, entry.Id);

            Assert.Equal(first.Id, second.Id);
            Assert.Equal(2, await ctx.CommissionLedgerEntries.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task Pay_EarnedEntry_CreatesWalletCommissionTx()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            Guid attributionId = await SeedAttributionAsync(ctx);
            await SeedCommissionRuleAsync(ctx);
            var booking = await CompleteBookingAsync(ctx, attributionId);
            var commissionSvc = BuildCommissionService(ctx);
            var entry = await commissionSvc.FinalizeCommissionForBookingAsync(BookingTestData.TenantId, booking.Id);
            Guid salesmanId = (await ctx.AttributionSessions.IgnoreQueryFilters().FirstAsync(s => s.Id == attributionId)).SalesmanId!.Value;

            var paid = await commissionSvc.PayCommissionAsync(BookingTestData.TenantId, entry!.Id);

            Assert.Equal(CommissionLedgerState.Paid, paid.State);
            Assert.NotNull(paid.PaidAt);
            Assert.NotNull(paid.WalletTransactionId);

            var walletTx = await ctx.WalletTransactions.IgnoreQueryFilters().FirstAsync(w => w.Id == paid.WalletTransactionId.Value);
            Assert.Equal(WalletTransactionType.Commission, walletTx.Type);
            Assert.Equal(salesmanId, walletTx.OwnerId);
            Assert.Equal(30_000m, walletTx.Amount);
        }

        [Fact]
        public async Task Reverse_PaidEntry_CreatesWalletReversalTx()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            Guid attributionId = await SeedAttributionAsync(ctx);
            await SeedCommissionRuleAsync(ctx);
            var booking = await CompleteBookingAsync(ctx, attributionId);
            var commissionSvc = BuildCommissionService(ctx);
            var entry = await commissionSvc.FinalizeCommissionForBookingAsync(BookingTestData.TenantId, booking.Id);
            await commissionSvc.PayCommissionAsync(BookingTestData.TenantId, entry!.Id);

            var reversal = await commissionSvc.ReverseCommissionAsync(BookingTestData.TenantId, entry.Id);

            var walletTx = await ctx.WalletTransactions.IgnoreQueryFilters()
                .FirstAsync(w => w.Type == WalletTransactionType.Reversal);
            Assert.Equal(-30_000m, walletTx.Amount);
            Assert.Equal(entry.WalletTransactionId, walletTx.RelatedTransactionId);
            Assert.Equal(CommissionLedgerState.Reversed, reversal.State);
        }

        [Fact]
        public async Task GetLedger_SalesmanScoped_NoCrossTenantLeak()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            Guid attributionId = await SeedAttributionAsync(ctx);
            await SeedCommissionRuleAsync(ctx);
            var booking = await CompleteBookingAsync(ctx, attributionId);
            var commissionSvc = BuildCommissionService(ctx);
            await commissionSvc.FinalizeCommissionForBookingAsync(BookingTestData.TenantId, booking.Id);

            // Tenant A: ledger có 1 entry.
            var ledgerA = await commissionSvc.GetLedgerAsync(BookingTestData.TenantId);
            Assert.Single(ledgerA);

            // Tenant B: không thấy entry của A (isolation).
            var ledgerB = await commissionSvc.GetLedgerAsync(BookingTestData.OtherTenantId);
            Assert.Empty(ledgerB);
        }

        [Fact]
        public async Task Finalize_WithholdingApplied_Above5M()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            Guid attributionId = await SeedAttributionAsync(ctx);
            await SeedCommissionRuleAsync(ctx, type: CommissionType.FixedAmount, value: 6_000_000m);   // gross = 6tr ≥ 5tr
            var booking = await CompleteBookingAsync(ctx, attributionId);

            var entry = await BuildCommissionService(ctx).FinalizeCommissionForBookingAsync(BookingTestData.TenantId, booking.Id);

            Assert.Equal(600_000m, entry!.TaxWithheldAmount);
            Assert.Equal(5_400_000m, entry.NetCommissionAmount);
            Assert.Equal("ND253-2026-10PCT-5M", entry.WithholdingReasonCode);
        }

        // ── Fake IWalletService (chỉ CreateTransactionAsync hoạt động — phần còn lại không dùng) ──
        private sealed class FakeWalletService(VanAnDbContext ctx) : IWalletService
        {
            public Task<WalletTransaction> CreateTransactionAsync(Guid ownerId, WalletTransactionType type, decimal amount, string description,
                Guid? relatedOrderId = null, Guid? relatedTransactionId = null, TenantId? tenantIdOverride = null)
            {
                TenantId tenantId = tenantIdOverride ?? new TenantId(Guid.Empty);
                decimal balanceBefore = ctx.WalletTransactions.IgnoreQueryFilters()
                    .Where(w => w.OwnerId == ownerId)
                    .OrderByDescending(w => w.CreatedAt)
                    .Select(w => (decimal?)w.BalanceAfter)
                    .FirstOrDefault() ?? 0m;
                var tx = new WalletTransaction(tenantId, ownerId, type, amount, balanceBefore, description, relatedOrderId, relatedTransactionId);
                ctx.WalletTransactions.Add(tx);
                ctx.SaveChanges();
                return Task.FromResult(tx);
            }

            public Task<decimal> GetBalanceAsync(Guid ownerId) => throw new NotImplementedException();
            public Task<WalletSummaryDto> GetWalletAsync(Guid ownerId) => throw new NotImplementedException();
            public Task<WalletTransaction> ConfirmCodAsync(Guid shipperId, Guid orderId, decimal amount) => throw new NotImplementedException();
            public Task<WalletTransaction> ConfirmAdvanceAsync(Guid shipperId, Guid orderId, decimal amount) => throw new NotImplementedException();
            public Task<WalletTransaction> ConfirmAdvanceReceivedAsync(Guid shopOwnerId, Guid advanceTransactionId) => throw new NotImplementedException();
            public Task<List<PendingAdvanceDto>> GetPendingAdvancesAsync(Guid shopOwnerId) => throw new NotImplementedException();
            public Task<WalletTransaction> ReverseTransactionAsync(Guid ownerId, Guid originalTransactionId) => throw new NotImplementedException();
            public Task<WalletTransaction> ConfirmExternalPaymentAsync(Guid orderId, decimal amount, string paymentRef) => throw new NotImplementedException();
            public Task<WalletTransaction> SpendCommunityFundAsync(decimal amount, string reason, Guid approvedBy) => throw new NotImplementedException();
            public Task<WalletTransaction> RemitCodAsync(Guid shipperId, Guid orderId) => throw new NotImplementedException();
            public Task<List<PendingRemittanceDto>> GetPendingRemittancesAsync(Guid shipperId) => throw new NotImplementedException();
            public Task<VanAn.Shared.Domain.Aggregates.WalletAggregate.WithdrawalRequest> RequestWithdrawalAsync(Guid ownerId, decimal amount) => throw new NotImplementedException();
            public Task<List<WithdrawalRequestDto>> GetWithdrawalsAsync(Guid ownerId) => throw new NotImplementedException();
            public Task CancelWithdrawalAsync(Guid ownerId, Guid requestId) => throw new NotImplementedException();
            public Task<WithdrawalRequestListResult> GetWithdrawalRequestsAsync(VanAn.Shared.Domain.Aggregates.WalletAggregate.WithdrawalStatus? status, int page, int pageSize) => throw new NotImplementedException();
            public Task ApproveWithdrawalAsync(Guid requestId, Guid adminId) => throw new NotImplementedException();
            public Task RejectWithdrawalAsync(Guid requestId, Guid adminId, string reason) => throw new NotImplementedException();
            public Task<VanAn.Shared.Domain.Aggregates.WalletAggregate.WithdrawalRequest> MarkWithdrawalPaidAsync(Guid requestId, Guid adminId, string bankReference) => throw new NotImplementedException();
        }
    }
}
