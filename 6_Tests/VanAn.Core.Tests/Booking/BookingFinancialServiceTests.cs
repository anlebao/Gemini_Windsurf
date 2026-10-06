using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
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
    /// IBookingFinancialService — deposit classification §16.2 (theo tax profile, KHÔNG hard-code),
    /// PaymentTransaction 7-state §16.3, InvoiceIntegrationRecord + trigger snapshot §16.4,
    /// outbox events §16.6 (DepositPaid/PaymentCaptured/ServiceCompleted/InvoiceRequired/InvoiceFailed/RefundCompleted).
    /// </summary>
    public class BookingFinancialServiceTests
    {
        private static BookingFinancialService BuildFinancialService(VanAnDbContext ctx)
            => new(ctx, new OutboxRepository(ctx), NullLogger<BookingFinancialService>.Instance);

        private static BookingService BuildBookingService(VanAnDbContext ctx)
            => new(ctx, new AvailabilityService(ctx, NullLogger<AvailabilityService>.Instance), NullLogger<BookingService>.Instance);

        private static async Task<(VanAnDbContext Ctx, BookingFinancialService Fin, BookingEntity Booking)> SeedBookingAsync(
            VanAnDbContext ctx, DepositPolicy policy = DepositPolicy.None, decimal? fixedAmount = null, string? einvoiceMode = null)
        {
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var config = await BookingTestData.EnsureConfigWithDepositAsync(ctx, BookingTestData.TenantId, policy, fixedAmount);
            config.UpdatePolicyText("Chính sách hủy", einvoiceMode);
            await ctx.SaveChangesAsync();

            var bookingSvc = BuildBookingService(ctx);
            var (_, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);
            var booking = await bookingSvc.CreateBookingAsync(BookingTestData.TenantId,
                new CreateBookingCommand(OfferingId: offeringId, StartAt: BookingTestData.At(14, 0), StaffId: null, null, "device-fin", null, [], AttributionId: null),
                "key-fin");
            return (ctx, BuildFinancialService(ctx), booking);
        }

        private static Task<int> CountOutboxAsync(VanAnDbContext ctx, string eventType)
            => ctx.OutboxMessages.IgnoreQueryFilters().CountAsync(m => m.EventType == eventType);

        // ── Classification §16.2 ──────────────────────────────────────────────

        [Fact]
        public void ResolveDepositNature_Default_SecurityDeposit()
        {
            var config = new BookingTenantConfig(BookingTestData.TenantId);

            Assert.Equal(MoneyNatureType.SecurityDeposit, BuildFinancialService(new VanAnDbContext(new DbContextOptions<VanAnDbContext>(), null!)).ResolveDepositNature(config));
        }

        [Fact]
        public void ResolveDepositNature_PrepayMode_PrepaymentForService()
        {
            var config = new BookingTenantConfig(BookingTestData.TenantId);
            config.UpdatePolicyText(null, "DEPOSIT_IS_PREPAYMENT");

            Assert.Equal(MoneyNatureType.PrepaymentForService,
                BuildFinancialService(new VanAnDbContext(new DbContextOptions<VanAnDbContext>(), null!)).ResolveDepositNature(config));
        }

        [Fact]
        public void ResolveInvoiceTrigger_Mapping()
        {
            var fin = BuildFinancialService(new VanAnDbContext(new DbContextOptions<VanAnDbContext>(), null!));
            var config = new BookingTenantConfig(BookingTestData.TenantId);

            Assert.Equal(InvoiceTrigger.OnPayment, fin.ResolveInvoiceTrigger(MoneyNatureType.PrepaymentForService, config));
            Assert.Equal(InvoiceTrigger.ExternalRule, fin.ResolveInvoiceTrigger(MoneyNatureType.SecurityDeposit, config));
            Assert.Equal(InvoiceTrigger.OnCompletion, fin.ResolveInvoiceTrigger(MoneyNatureType.FinalPayment, config));
        }

        // ── Deposit capture ───────────────────────────────────────────────────

        [Fact]
        public async Task CaptureDeposit_WithProviderRef_PaidAndOutboxDepositPaid()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var (ctx, fin, booking) = await SeedBookingAsync(scope.Context, DepositPolicy.Fixed, 100_000m);

            var tx = await fin.CaptureDepositAsync(BookingTestData.TenantId, booking.Id, 100_000m, "VIETQR", "ref-1");

            Assert.Equal(PaymentStatus.Paid, tx.Status);
            Assert.Equal("ref-1", tx.ProviderRef);
            Assert.Equal(MoneyNatureType.SecurityDeposit, tx.Type);

            var reloaded = await ctx.Bookings.IgnoreQueryFilters().FirstAsync(b => b.Id == booking.Id);
            Assert.Equal(PaymentStatus.Paid, reloaded.PaymentStatus);
            Assert.Equal(BookingSubState.DepositPaid, reloaded.SubState);

            Assert.Equal(1, await CountOutboxAsync(ctx, "DepositPaid"));
            Assert.Equal(0, await CountOutboxAsync(ctx, "InvoiceRequired"));   // SecurityDeposit → ExternalRule, không auto
        }

        [Fact]
        public async Task CaptureDeposit_NoProviderRef_Pending()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var (ctx, fin, booking) = await SeedBookingAsync(scope.Context, DepositPolicy.Fixed, 100_000m);

            var tx = await fin.CaptureDepositAsync(BookingTestData.TenantId, booking.Id, 100_000m);

            Assert.Equal(PaymentStatus.Pending, tx.Status);
            Assert.Equal(0, await CountOutboxAsync(ctx, "DepositPaid"));

            var confirmed = await fin.ConfirmDepositAsync(BookingTestData.TenantId, booking.Id, tx.Id, "ref-2");
            Assert.Equal(PaymentStatus.Paid, confirmed.Status);
            Assert.Equal(1, await CountOutboxAsync(ctx, "DepositPaid"));
        }

        [Fact]
        public async Task CaptureDeposit_PrepayMode_EmitsInvoiceRequired()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            // Prepay mode → deposit = PREPAYMENT_FOR_SERVICE → trigger OnPayment → InvoiceRequired tại thu tiền (NĐ 70/2025).
            var (ctx, fin, booking) = await SeedBookingAsync(scope.Context, DepositPolicy.Fixed, 100_000m, einvoiceMode: "DEPOSIT_IS_PREPAYMENT");

            var tx = await fin.CaptureDepositAsync(BookingTestData.TenantId, booking.Id, 100_000m, "VIETQR", "ref-3");

            Assert.Equal(MoneyNatureType.PrepaymentForService, tx.Type);
            Assert.Equal(1, await CountOutboxAsync(ctx, "DepositPaid"));
            Assert.Equal(1, await CountOutboxAsync(ctx, "InvoiceRequired"));

            var record = await ctx.InvoiceIntegrationRecords.IgnoreQueryFilters().FirstAsync(r => r.BookingId == booking.Id);
            Assert.Equal(BookingInvoiceStatus.Pending, record.InvoiceStatus);
            Assert.Equal(InvoiceTrigger.OnPayment, record.InvoiceTrigger);
        }

        [Fact]
        public async Task CaptureFinalPayment_PaidAndOutbox()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var (ctx, fin, booking) = await SeedBookingAsync(scope.Context);

            var tx = await fin.CaptureFinalPaymentAsync(BookingTestData.TenantId, booking.Id, 300_000m, "CASH", "cash-1");

            Assert.Equal(MoneyNatureType.FinalPayment, tx.Type);
            Assert.Equal(PaymentStatus.Paid, tx.Status);
            Assert.Equal(1, await CountOutboxAsync(ctx, "PaymentCaptured"));

            var reloaded = await ctx.Bookings.IgnoreQueryFilters().FirstAsync(b => b.Id == booking.Id);
            Assert.Equal(PaymentStatus.Paid, reloaded.PaymentStatus);
        }

        [Fact]
        public async Task ServiceCompleted_OnCompletion_EmitsInvoiceRequired()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var (ctx, fin, booking) = await SeedBookingAsync(scope.Context);

            await fin.RecordServiceCompletedAsync(BookingTestData.TenantId, booking.Id);

            Assert.Equal(1, await CountOutboxAsync(ctx, "ServiceCompleted"));
            Assert.Equal(1, await CountOutboxAsync(ctx, "InvoiceRequired"));   // FinalPayment → OnCompletion → hóa đơn tại hoàn thành

            var record = await ctx.InvoiceIntegrationRecords.IgnoreQueryFilters().FirstAsync(r => r.BookingId == booking.Id);
            Assert.Equal(BookingInvoiceStatus.Pending, record.InvoiceStatus);
            Assert.Equal(InvoiceTrigger.OnCompletion, record.InvoiceTrigger);
        }

        [Fact]
        public async Task RefundDeposit_EmitsRefundCompleted()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var (ctx, fin, booking) = await SeedBookingAsync(scope.Context, DepositPolicy.Fixed, 100_000m);
            var tx = await fin.CaptureDepositAsync(BookingTestData.TenantId, booking.Id, 100_000m, "VIETQR", "ref-r");

            var refunded = await fin.RefundDepositAsync(BookingTestData.TenantId, booking.Id, "ref-rb");

            Assert.Equal(PaymentStatus.Refunded, refunded.Status);
            Assert.Equal(1, await CountOutboxAsync(ctx, "RefundCompleted"));

            // Refund lần 2 → không còn deposit Paid → throw.
            await Assert.ThrowsAsync<ValidationException>(() => fin.RefundDepositAsync(BookingTestData.TenantId, booking.Id));
        }

        [Fact]
        public async Task UpdateInvoiceStatus_IssuedAndFailed_Outbox()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var (ctx, fin, booking) = await SeedBookingAsync(scope.Context);

            var record = await fin.UpdateInvoiceStatusAsync(BookingTestData.TenantId, booking.Id, BookingInvoiceStatus.Issued, "VNP", "INV-001");
            Assert.Equal(BookingInvoiceStatus.Issued, record.InvoiceStatus);
            Assert.Equal("INV-001", record.Reference);
            Assert.Equal(1, await CountOutboxAsync(ctx, "InvoiceIssued"));

            await fin.UpdateInvoiceStatusAsync(BookingTestData.TenantId, booking.Id, BookingInvoiceStatus.Failed, "VNP", null, "E101");
            Assert.Equal(1, await CountOutboxAsync(ctx, "InvoiceFailed"));

            var reloaded = await ctx.Bookings.IgnoreQueryFilters().FirstAsync(b => b.Id == booking.Id);
            Assert.Equal(BookingInvoiceStatus.Failed, reloaded.InvoiceStatus);
        }

        [Fact]
        public async Task GetDeposit_ReturnsDepositOnly()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var (ctx, fin, booking) = await SeedBookingAsync(scope.Context, DepositPolicy.Fixed, 100_000m);
            await fin.CaptureDepositAsync(BookingTestData.TenantId, booking.Id, 100_000m, "VIETQR", "ref-d");
            await fin.CaptureFinalPaymentAsync(BookingTestData.TenantId, booking.Id, 200_000m, "CASH", "cash-d");

            var deposit = await fin.GetDepositAsync(BookingTestData.TenantId, booking.Id);

            Assert.NotNull(deposit);
            Assert.Equal(MoneyNatureType.SecurityDeposit, deposit!.Type);
        }
    }
}
