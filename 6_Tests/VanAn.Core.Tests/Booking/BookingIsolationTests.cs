using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Booking;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;
using BookingEntity = VanAn.Shared.Domain.Booking;
using NotFoundException = VanAn.CoreHub.Services.NotFoundException;
using ValidationException = VanAn.CoreHub.Services.ValidationException;

namespace VanAn.Core.Tests.BookingScheduling
{
    /// <summary>
    /// P6.2 — Tenant isolation matrix SRS §18.3 (5 negative cases bắt buộc):
    ///   1. Tenant A không đọc booking Tenant B
    ///   2. Tenant A không assign staff Tenant B
    ///   3. QR Tenant A không tạo booking cho Tenant B (Risk 5 — KHÔNG tin tenant từ client)
    ///   4. salesman A không xem commission của salesman B ngoài scope được cấp
    ///   5. public booking token của Tenant A không resolve dữ liệu Tenant B
    /// + Audit completeness §24 — mọi transition ghi BookingEvent (before/after).
    /// </summary>
    public class BookingIsolationTests
    {
        private static BookingService BuildBookingService(VanAnDbContext ctx)
            => new(ctx, new AvailabilityService(ctx, NullLogger<AvailabilityService>.Instance), NullLogger<BookingService>.Instance);

        private static BookingService BuildBookingServiceWithCommission(VanAnDbContext ctx)
            => new(ctx, new AvailabilityService(ctx, NullLogger<AvailabilityService>.Instance), NullLogger<BookingService>.Instance,
                orderService: null, financialService: null,
                commissionService: new CommissionService(ctx, new TaxWithholdingPolicyAdapter(), new FakeWalletService(ctx), NullLogger<CommissionService>.Instance));

        private static CommissionService BuildCommissionService(VanAnDbContext ctx)
            => new(ctx, new TaxWithholdingPolicyAdapter(), new FakeWalletService(ctx), NullLogger<CommissionService>.Instance);

        private static CreateBookingCommand Command(Guid offeringId, DateTime startAt, Guid? staffId = null, Guid? attributionId = null)
            => new(offeringId, startAt, staffId, null, "device-iso", "Note", [], attributionId);

        // ── §18.3 case 1: Tenant A không đọc booking Tenant B ─────────────────

        [Fact]
        public async Task TenantA_CannotRead_BookingOfTenantB()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildBookingService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.OtherTenantId, enabled: true);
            var (staffA, offeringA) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);
            var (staffB, offeringB) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.OtherTenantId);

            // Booking của tenant B (tạo qua service — tenantId B).
            var bookingB = await svc.CreateBookingAsync(BookingTestData.OtherTenantId,
                Command(offeringB, BookingTestData.At(14, 0)), "iso-b1");

            // Tenant A: đọc trực tiếp booking B → null; queue → rỗng; transition → NotFound.
            Assert.Null(await svc.GetBookingAsync(BookingTestData.TenantId, bookingB.Id));
            Assert.Empty(await svc.GetQueueAsync(BookingTestData.TenantId));
            await Assert.ThrowsAsync<NotFoundException>(() =>
                svc.ConfirmAsync(BookingTestData.TenantId, bookingB.Id));

            // Đảm bảo booking B vẫn tồn tại trong tenant B (không bị corrupt bởi query A).
            Assert.NotNull(await svc.GetBookingAsync(BookingTestData.OtherTenantId, bookingB.Id));
        }

        // ── §18.3 case 2: Tenant A không assign staff Tenant B ───────────────

        [Fact]
        public async Task TenantA_CannotAssign_StaffOfTenantB()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildBookingService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (_, offeringA) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);
            var (staffB, _) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.OtherTenantId);

            var bookingA = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringA, BookingTestData.At(14, 0)), "iso-b2");

            // Assign staff thuộc tenant B vào booking của tenant A → conflict (§13 re-check server-side).
            var ex = await Assert.ThrowsAsync<BookingConflictException>(() =>
                svc.AssignStaffAsync(BookingTestData.TenantId, bookingA.Id, staffB));
            Assert.Contains("Staff", ex.Message, StringComparison.OrdinalIgnoreCase);

            // KHÔNG có assignment record nào được tạo.
            Assert.Empty(await ctx.BookingStaffAssignments.IgnoreQueryFilters()
                .Where(a => a.BookingId == bookingA.Id).ToListAsync());
            Assert.Null(bookingA.StaffId);
        }

        // ── §18.3 case 3: QR Tenant A không tạo booking cho Tenant B ──────────

        [Fact]
        public async Task QrOfTenantA_CannotCreateBooking_ForTenantB()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildBookingService(ctx);

            // QR + attribution session thuộc tenant A (qualified).
            await BookingTestData.SeedActiveTenantAsync(ctx, BookingTestData.TenantId);
            Guid salesmanA = await BookingTestData.SeedSalesmanAsync(ctx, BookingTestData.TenantId);
            var qrSvc = new QRAttributionService(ctx, NullLogger<QRAttributionService>.Instance);
            await qrSvc.CreateQrChannelAsync(BookingTestData.TenantId, "iso-tok-a", salesmanA);
            var resolved = await qrSvc.ResolveQrAsync("iso-tok-a", "anon-iso");
            Guid attributionA = resolved.AttributionSessionId;

            // Tenant B enable + offering.
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.OtherTenantId, enabled: true);
            var (_, offeringB) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.OtherTenantId);

            // Dùng attribution của tenant A để tạo booking cho tenant B → bị TỪ CHỐI (Risk 5).
            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                svc.CreateBookingAsync(BookingTestData.OtherTenantId,
                    Command(offeringB, BookingTestData.At(14, 0), attributionId: attributionA), "iso-b3"));
            Assert.Contains("giới thiệu", ex.Message);

            // KHÔNG có booking nào được tạo.
            Assert.Empty(await ctx.Bookings.IgnoreQueryFilters().ToListAsync());

            // Attribution hợp lệ cho CHÍNH tenant A vẫn hoạt động bình thường.
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (_, offeringA) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);
            var bookingA = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringA, BookingTestData.At(15, 0), attributionId: attributionA), "iso-b3b");
            Assert.Equal(attributionA, bookingA.AttributionId);
        }

        // ── §18.3 case 4: salesman A không xem commission salesman B ─────────

        [Fact]
        public async Task SalesmanA_CannotView_CommissionOfSalesmanB()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var commissionSvc = BuildCommissionService(ctx);

            // 2 salesman cùng tenant — mỗi salesman 1 QR + 1 booking hoàn tất.
            Guid salesmanA = await BookingTestData.SeedSalesmanAsync(ctx, BookingTestData.TenantId, "Salesman A");
            Guid salesmanB = await BookingTestData.SeedSalesmanAsync(ctx, BookingTestData.TenantId, "Salesman B");
            var qrSvc = new QRAttributionService(ctx, NullLogger<QRAttributionService>.Instance);
            await BookingTestData.SeedActiveTenantAsync(ctx, BookingTestData.TenantId);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);

            var qrA = await qrSvc.CreateQrChannelAsync(BookingTestData.TenantId, "iso-ca", salesmanA);
            var qrB = await qrSvc.CreateQrChannelAsync(BookingTestData.TenantId, "iso-cb", salesmanB);
            var attrA = await qrSvc.ResolveQrAsync("iso-ca", "anon-ca");
            var attrB = await qrSvc.ResolveQrAsync("iso-cb", "anon-cb");

            ctx.CommissionRules.Add(new CommissionRule(BookingTestData.TenantId, CommissionType.Percentage, 10m));
            await ctx.SaveChangesAsync();

            var bookingSvc = BuildBookingService(ctx);
            var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            BookingEntity bookingA = await CreateAndCompleteAsync(bookingSvc, ctx, offeringId, staffId, attrA.AttributionSessionId, "iso-k1");
            BookingEntity bookingB = await CreateAndCompleteAsync(bookingSvc, ctx, offeringId, staffId, attrB.AttributionSessionId, "iso-k2", startHour: 16);

            await commissionSvc.FinalizeCommissionForBookingAsync(BookingTestData.TenantId, bookingA.Id);
            await commissionSvc.FinalizeCommissionForBookingAsync(BookingTestData.TenantId, bookingB.Id);

            // Ledger tổng có 2 entries (1/salesman).
            Assert.Equal(2, (await commissionSvc.GetLedgerAsync(BookingTestData.TenantId)).Count);

            // Salesman A chỉ thấy entry của A — KHÔNG thấy entry của salesman B (§18.3 scope được cấp).
            var ledgerA = await commissionSvc.GetLedgerAsync(BookingTestData.TenantId, salesmanId: salesmanA);
            Assert.Single(ledgerA);
            Assert.Equal(salesmanA, ledgerA[0].SalesmanId);
            Assert.All(ledgerA, e => Assert.Equal(salesmanA, e.SalesmanId));

            // Salesman B chỉ thấy entry của B.
            var ledgerB = await commissionSvc.GetLedgerAsync(BookingTestData.TenantId, salesmanId: salesmanB);
            Assert.Single(ledgerB);
            Assert.Equal(salesmanB, ledgerB[0].SalesmanId);
        }

        // ── §18.3 case 5: public token Tenant A không resolve dữ liệu Tenant B ─

        [Fact]
        public async Task PublicTokenOfTenantA_DoesNotResolve_DataOfTenantB()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildBookingService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.OtherTenantId, enabled: true);
            var (_, offeringA) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);
            var (_, offeringB) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.OtherTenantId);

            var bookingA = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringA, BookingTestData.At(14, 0)), "iso-p1");
            var bookingB = await svc.CreateBookingAsync(BookingTestData.OtherTenantId,
                Command(offeringB, BookingTestData.At(16, 0)), "iso-p2");

            // Token của A chỉ resolve booking A — không lộ dữ liệu B.
            var statusA = await svc.GetPublicStatusAsync(bookingA.PublicBookingCode);
            Assert.NotNull(statusA);
            Assert.Equal(bookingA.Id, statusA!.BookingId);
            Assert.Equal(bookingA.PublicBookingCode, statusA.PublicBookingCode);
            Assert.Equal("Massage 60m", statusA.OfferingNameSnapshot);
            Assert.NotEqual(bookingB.PublicBookingCode, statusA.PublicBookingCode);

            // Token B resolve booking B (không dính booking A).
            var statusB = await svc.GetPublicStatusAsync(bookingB.PublicBookingCode);
            Assert.NotNull(statusB);
            Assert.Equal(bookingB.Id, statusB!.BookingId);

            // Token không tồn tại → null (không lộ gì).
            Assert.Null(await svc.GetPublicStatusAsync("iso-nonexistent"));
        }

        // ── §24 Audit completeness — mọi transition ghi BookingEvent ─────────

        [Fact]
        public async Task Audit_EveryTransition_EmitsBookingEvent()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildBookingService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);
            // Staff thứ 2 cùng skill (link tới CHÍNH offering trên) — để change staff (reassign) tạo event riêng.
            var (staff2, _) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId, staffName: "KTV B");
            ctx.StaffServices.Add(new VanAn.Shared.Domain.StaffService(BookingTestData.TenantId, staff2, offeringId));
            await ctx.SaveChangesAsync();

            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(14, 0)), "iso-audit");
            await svc.ConfirmAsync(BookingTestData.TenantId, booking.Id, actorId: Guid.NewGuid());
            await svc.AssignStaffAsync(BookingTestData.TenantId, booking.Id, staffId, actorId: Guid.NewGuid());
            await svc.ChangeStaffAsync(BookingTestData.TenantId, booking.Id, staff2, actorId: Guid.NewGuid());
            await svc.CheckInAsync(BookingTestData.TenantId, booking.Id, actorId: Guid.NewGuid());
            await svc.StartServiceAsync(BookingTestData.TenantId, booking.Id, actorId: Guid.NewGuid());
            await svc.CompleteAsync(BookingTestData.TenantId, booking.Id, 300_000m, actorId: Guid.NewGuid());

            // BookingEvent cho từng transition (SRS §23-24 — before/after, actor).
            var events = await ctx.BookingEvents.IgnoreQueryFilters()
                .Where(e => e.BookingId == booking.Id)
                .OrderBy(e => e.CreatedAt)
                .ToListAsync();

            Assert.Contains(events, e => e.EventType == "BookingCreated");
            Assert.Contains(events, e => e.EventType == "BookingConfirmed");
            Assert.Contains(events, e => e.EventType == "BookingStaffAssigned");
            Assert.Contains(events, e => e.EventType == "BookingStaffReassigned");
            Assert.Contains(events, e => e.EventType == "BookingCheckedIn");
            Assert.Contains(events, e => e.EventType == "BookingStarted");
            Assert.Contains(events, e => e.EventType == "BookingCompleted");

            // Thứ tự đúng (audit trail linear).
            string[] types = events.Select(e => e.EventType).ToArray();
            int idxCreated = Array.IndexOf(types, "BookingCreated");
            int idxConfirmed = Array.IndexOf(types, "BookingConfirmed");
            int idxCompleted = Array.IndexOf(types, "BookingCompleted");
            Assert.True(idxCreated < idxConfirmed && idxConfirmed < idxCompleted);

            // Mọi event có actor (TENANT) sau create (create = CUSTOMER).
            Assert.All(events.Where(e => e.EventType != "BookingCreated"),
                e => Assert.Equal("TENANT", e.ActorType));
        }

        // ── P6 hardening: commission auto-finalize tại COMPLETED (P3.2 §17.1) ─

        [Fact]
        public async Task Complete_WithCommissionWired_AutoFinalizesEarnedEntry()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildBookingServiceWithCommission(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            await BookingTestData.SeedActiveTenantAsync(ctx, BookingTestData.TenantId);
            Guid salesmanId = await BookingTestData.SeedSalesmanAsync(ctx, BookingTestData.TenantId);
            var qrSvc = new QRAttributionService(ctx, NullLogger<QRAttributionService>.Instance);
            await qrSvc.CreateQrChannelAsync(BookingTestData.TenantId, "iso-fin-qr", salesmanId);
            var resolved = await qrSvc.ResolveQrAsync("iso-fin-qr", "anon-fin");
            ctx.CommissionRules.Add(new CommissionRule(BookingTestData.TenantId, CommissionType.Percentage, 10m));
            await ctx.SaveChangesAsync();
            var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                new CreateBookingCommand(offeringId, BookingTestData.At(14, 0), null, null, "dev-fin", "Note", [], resolved.AttributionSessionId),
                "iso-fin-k");
            await svc.ConfirmAsync(BookingTestData.TenantId, booking.Id);
            await svc.AssignStaffAsync(BookingTestData.TenantId, booking.Id, staffId);
            await svc.CheckInAsync(BookingTestData.TenantId, booking.Id);
            await svc.StartServiceAsync(BookingTestData.TenantId, booking.Id);
            await svc.CompleteAsync(BookingTestData.TenantId, booking.Id, 300_000m);

            // COMPLETED → ledger tự finalize EARNED (idempotent — complete 2 lần không tạo entry thứ 2).
            var entries = await ctx.CommissionLedgerEntries.IgnoreQueryFilters()
                .Where(e => e.BookingId == booking.Id).ToListAsync();
            Assert.Single(entries);
            Assert.Equal(CommissionLedgerState.Earned, entries[0].State);
            Assert.Equal(salesmanId, entries[0].SalesmanId);
            Assert.Equal(30_000m, entries[0].GrossCommissionAmount);

            await svc.CompleteAsync(BookingTestData.TenantId, booking.Id, 300_000m);
            Assert.Equal(1, await ctx.CommissionLedgerEntries.IgnoreQueryFilters()
                .CountAsync(e => e.BookingId == booking.Id));
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static async Task<BookingEntity> CreateAndCompleteAsync(
            BookingService svc, VanAnDbContext ctx, Guid offeringId, Guid staffId, Guid attributionId,
            string key, int startHour = 14)
        {
            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                new CreateBookingCommand(offeringId, BookingTestData.At(startHour, 0), null, null, $"dev-{key}", "Note", [], attributionId),
                key);
            await svc.ConfirmAsync(BookingTestData.TenantId, booking.Id);
            await svc.AssignStaffAsync(BookingTestData.TenantId, booking.Id, staffId);
            await svc.CheckInAsync(BookingTestData.TenantId, booking.Id);
            await svc.StartServiceAsync(BookingTestData.TenantId, booking.Id);
            await svc.CompleteAsync(BookingTestData.TenantId, booking.Id, 300_000m);
            return booking;
        }

        // ── Fake IWalletService (chỉ CreateTransactionAsync hoạt động) ──────
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
