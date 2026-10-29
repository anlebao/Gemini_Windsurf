using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Booking;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;
using VanAn.Shared.Domain.Audit;
using VanAn.Shared.DTOs;
using Xunit;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.Core.Tests.BookingScheduling;

/// <summary>
/// Fix 2b (2026-10-14, user approve): BookingAccountingService — consumer DepositPaid/PaymentCaptured/
/// ServiceCompleted → AccountingEntry (511/3331; cọc = 3387 bản chất riêng; idempotent theo Reference;
/// HTX TT 71 → 512 nội bộ).
/// </summary>
public class BookingAccountingServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
    private static readonly Guid BookingId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid TxId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

    private readonly List<CoreAccountingEntry> _captured = [];
    private readonly Mock<IAccountingService> _accounting = new();
    private readonly Mock<IAccountingEntryRepository> _repo = new();
    private readonly Mock<IAuditTrailService> _audit = new();
    private readonly Mock<IPeriodClosingService> _closing = new();

    private BookingAccountingService BuildService(VanAnDbContext db)
    {
        _accounting.Setup(a => a.CreateRevenueEntryAsync(
                It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync((TenantId _, AccountingPeriod _, decimal amount, string desc,
                string? accountCode, string? reference, IndustrySector? sector, DateTime? txDate) =>
                new AccountingEntryDto
                {
                    Id = Guid.NewGuid(),
                    TenantId = Tenant.Value,
                    Amount = amount,
                    Description = desc,
                    AccountCode = accountCode,
                    Reference = reference,
                    EntryType = AccountingEntryType.Revenue,
                    TransactionDate = txDate ?? DateTime.UtcNow
                });
        _repo.Setup(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<CoreAccountingEntry, CancellationToken>((e, _) => _captured.Add(e));
        _audit.Setup(a => a.LogCreateAsync(It.IsAny<AuditableEntityType>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLog?)null);
        _closing.Setup(c => c.GetPeriodStatusAsync(It.IsAny<AccountingPeriod>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PeriodClosingStatus.Open);
        return new BookingAccountingService(db, _accounting.Object, _repo.Object, _closing.Object, _audit.Object,
            NullLogger<BookingAccountingService>.Instance);
    }

    /// <summary>Repo trả Exists=true cho reference chỉ định (idempotency theo Reference).</summary>
    private void RevenueAlreadyExists(bool exists)
        => _repo.Setup(r => r.ExistsByReferenceAsync(It.IsAny<TenantId>(), $"Booking:{BookingId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists);

    // ── ServiceCompleted → doanh thu 511 + 3331 (anchor SRS) ────────────────

    [Fact]
    public async Task ServiceCompleted_CreatesRevenueAndVat()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        var svc = BuildService(scope.Context);
        RevenueAlreadyExists(false);

        await svc.RecordServiceCompletedAsync(Tenant, BookingId, 300_000m, new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc));

        _accounting.Verify(a => a.CreateRevenueEntryAsync(
            Tenant, new AccountingPeriod(2026, 10), 300_000m, It.IsAny<string>(),
            "511", $"Booking:{BookingId}", It.IsAny<IndustrySector?>(), new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc)), Times.Once);
        _accounting.Verify(a => a.CreateRevenueEntryAsync(
            Tenant, new AccountingPeriod(2026, 10), 30_000m, It.IsAny<string>(),
            "3331", $"Booking:{BookingId}", It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>()), Times.Once);
    }

    [Fact]
    public async Task ServiceCompleted_RevenueAlreadyExists_Skips()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        var svc = BuildService(scope.Context);
        RevenueAlreadyExists(true);   // PaymentCaptured đã ghi nhận trước

        await svc.RecordServiceCompletedAsync(Tenant, BookingId, 300_000m, DateTime.UtcNow);

        _accounting.Verify(a => a.CreateRevenueEntryAsync(
            It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>()), Times.Never);
    }

    // ── Issue #195 follow-up (2026-10-29, user approve A): booking có Order gắn kèm (D2) →
    //    doanh thu chuyển hẳn sang ORDER (ghi tại order payment — single source). ──

    [Fact]
    public async Task ServiceCompleted_BookingHasLinkedOrder_SkipsRevenue()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        var ctx = scope.Context;
        var booking = new Booking(Tenant, "RV-BK-1", Guid.NewGuid(), "Offering", 60, 300_000m,
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), 300_000m);
        booking.AttachOrder(Guid.NewGuid());
        ctx.Bookings.Add(booking);
        await ctx.SaveChangesAsync();

        var svc = BuildService(ctx);
        RevenueAlreadyExists(false);   // chưa có revenue booking — nhưng có order → vẫn skip

        await svc.RecordServiceCompletedAsync(Tenant, booking.Id, 300_000m, DateTime.UtcNow);

        _accounting.Verify(a => a.CreateRevenueEntryAsync(
            It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>()), Times.Never);
    }

    [Fact]
    public async Task FinalPayment_BookingHasLinkedOrder_SkipsRevenue()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        var ctx = scope.Context;
        var booking = new Booking(Tenant, "RV-BK-2", Guid.NewGuid(), "Offering", 60, 300_000m,
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), 300_000m);
        booking.AttachOrder(Guid.NewGuid());
        ctx.Bookings.Add(booking);
        await ctx.SaveChangesAsync();

        var svc = BuildService(ctx);
        RevenueAlreadyExists(false);

        await svc.RecordFinalPaymentAsync(Tenant, booking.Id, TxId, 200_000m, DateTime.UtcNow);

        _accounting.Verify(a => a.CreateRevenueEntryAsync(
            It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>()), Times.Never);
    }

    // ── DepositPaid → TK 3387 (bản chất riêng, KHÔNG doanh thu) ──────────────

    [Fact]
    public async Task DepositPaid_Creates3387_NotRevenue()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        var svc = BuildService(scope.Context);
        _repo.Setup(r => r.ExistsByReferenceAsync(Tenant, $"Booking:{BookingId}:Deposit:{TxId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await svc.RecordDepositPaidAsync(Tenant, BookingId, TxId, 100_000m, MoneyNatureType.PrepaymentForService,
            new DateTime(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc));

        var entry = Assert.Single(_captured);
        Assert.Equal("3387", entry.AccountCode);
        Assert.Equal(100_000m, entry.Amount);
        Assert.Equal($"Booking:{BookingId}:Deposit:{TxId}", entry.Reference);
        Assert.Equal(AccountingBookType.CashBankBook, entry.AccountingBookType);
        Assert.Equal(AccountingEntryType.Adjustment, entry.EntryType);
        // Bản chất riêng: KHÔNG tạo doanh thu.
        _accounting.Verify(a => a.CreateRevenueEntryAsync(
            It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>()), Times.Never);
    }

    [Fact]
    public async Task DepositPaid_DuplicateTransaction_Skips()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        var svc = BuildService(scope.Context);
        _repo.Setup(r => r.ExistsByReferenceAsync(Tenant, $"Booking:{BookingId}:Deposit:{TxId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await svc.RecordDepositPaidAsync(Tenant, BookingId, TxId, 100_000m, MoneyNatureType.SecurityDeposit, DateTime.UtcNow);

        Assert.Empty(_captured);
    }

    // ── PaymentCaptured → doanh thu (cash-basis; skip nếu đã có revenue) ─────

    [Fact]
    public async Task FinalPayment_BeforeCompletion_CreatesRevenue()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        var svc = BuildService(scope.Context);
        RevenueAlreadyExists(false);

        await svc.RecordFinalPaymentAsync(Tenant, BookingId, TxId, 200_000m, new DateTime(2026, 10, 12, 11, 0, 0, DateTimeKind.Utc));

        _accounting.Verify(a => a.CreateRevenueEntryAsync(
            Tenant, new AccountingPeriod(2026, 10), 200_000m, It.IsAny<string>(),
            "511", $"Booking:{BookingId}", It.IsAny<IndustrySector?>(), new DateTime(2026, 10, 12, 11, 0, 0, DateTimeKind.Utc)), Times.Once);
    }

    [Fact]
    public async Task FinalPayment_AfterCompletion_Skips_NoDoubleCount()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        var svc = BuildService(scope.Context);
        RevenueAlreadyExists(true);   // ServiceCompleted đã ghi nhận doanh thu

        await svc.RecordFinalPaymentAsync(Tenant, BookingId, TxId, 200_000m, DateTime.UtcNow);

        _accounting.Verify(a => a.CreateRevenueEntryAsync(
            It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>()), Times.Never);
    }

    // ── HTX (TT 71): tenant bán HTX + buyer là member → 512 (nội bộ) ─────────

    [Fact]
    public async Task ServiceCompleted_HtxInternalMember_Uses512()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        var ctx = scope.Context;

        // Seed HTX tenant (TT 71) + member tenant (buyer) + booking có CustomerId = member owner.
        var htx = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(Tenant, "HTX Bán Lẻ C");
        htx.MarkAsHtx();
        ctx.Tenants.Add(htx);

        var memberOwnerCustomerId = Guid.NewGuid();
        var memberTenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(
            new TenantId(Guid.NewGuid()), "HKD Thành Viên C");
        memberTenant.AssignOwnerCustomer(memberOwnerCustomerId);
        ctx.Tenants.Add(memberTenant);
        ctx.Members.Add(Member.CreateActive(Tenant, "MEM-001", MembershipType.OfficialMember,
            memberTenantId: memberTenant.Id, capitalContributionAmount: 1_000_000m));

        var booking = new Booking(Tenant, "BOOKINGTEST0001", Guid.NewGuid(), "Massage", 60, 300_000m,
            new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 12, 11, 0, 0, DateTimeKind.Utc),
            300_000m, customerId: memberOwnerCustomerId);
        typeof(VanAn.Shared.Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(booking, BookingId);
        ctx.Bookings.Add(booking);
        await ctx.SaveChangesAsync();

        var svc = BuildService(ctx);
        RevenueAlreadyExists(false);

        await svc.RecordServiceCompletedAsync(Tenant, BookingId, 300_000m, DateTime.UtcNow);

        _accounting.Verify(a => a.CreateRevenueEntryAsync(
            Tenant, It.IsAny<AccountingPeriod>(), 300_000m, It.IsAny<string>(),
            "512", $"Booking:{BookingId}", It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>()), Times.Once);
    }
}
