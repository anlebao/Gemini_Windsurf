using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services.Payment;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;
using Xunit;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.Integration.Tests.Accounting;

/// <summary>
/// TUÂN THỦ PHÁP LÝ P0.1 (2026-10-16): Kiểm soát thanh toán ≥ 20 triệu không dùng tiền mặt
/// (TT 219/2013/TT-BTC Điều 15.2.b) — EvaluateAsync thuần + GetCashPaymentReviewAsync
/// (gộp cùng ngày cùng NCC từ AccountingEntry.Vendor — SQLite in-memory).
/// </summary>
public class PaymentComplianceServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public PaymentComplianceServiceTests(TestDatabaseFixture fixture) => _fixture = fixture;

    private static PaymentComplianceService CreateService(
        IAccountingDbContext db,
        IOrderRepository? orderRepository = null) =>
        new(orderRepository ?? Mock.Of<IOrderRepository>(), db, NullLogger<PaymentComplianceService>.Instance);

    [Theory]
    [InlineData(20_000_000, "CASH", true)]
    [InlineData(21_000_000, "cash", true)]
    [InlineData(20_000_000, "COD", true)]
    [InlineData(19_999_999, "CASH", false)]
    [InlineData(20_000_000, "VIETQR", false)]
    [InlineData(50_000_000, "CREDIT_CARD", false)]
    public async Task EvaluateAsync_ThresholdLogic(decimal amount, string? method, bool expectedNeedsReview)
    {
        // Arrange
        PaymentComplianceService service = CreateService(_fixture.DbContext);

        // Act
        PaymentComplianceResult result = await service.EvaluateAsync(new TenantId(Guid.NewGuid()), amount, method);

        // Assert
        Assert.Equal(expectedNeedsReview, result.NeedsReview);
        Assert.Equal(20_000_000m, service.Threshold);
    }

    [Fact]
    public async Task GetCashPaymentReviewAsync_GroupsSameDaySameVendor()
    {
        // Arrange
        TenantId tenantId = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext db = _fixture.CreateFreshDbContext();

        AccountingPeriod period = new(2026, 7);

        // 2 phiếu mua cùng ngày 15/07 cùng NCC "Cty ABC" — tổng 24.000.000 ≥ ngưỡng
        db.AccountingEntries.Add(CoreAccountingEntry.CreateExpense(
            tenantId, period, new Money(12_000_000m, "VND"), "Mua hàng 1",
            accountCode: "642", vendor: "Cty ABC", transactionDate: new DateTime(2026, 7, 15)));
        db.AccountingEntries.Add(CoreAccountingEntry.CreateExpense(
            tenantId, period, new Money(12_000_000m, "VND"), "Mua hàng 2",
            accountCode: "642", vendor: "Cty ABC", transactionDate: new DateTime(2026, 7, 15)));
        // Cùng NCC khác ngày 16/07 — 10.000.000 < ngưỡng
        db.AccountingEntries.Add(CoreAccountingEntry.CreateExpense(
            tenantId, period, new Money(10_000_000m, "VND"), "Mua hàng 3",
            accountCode: "642", vendor: "Cty ABC", transactionDate: new DateTime(2026, 7, 16)));
        // NCC khác cùng ngày 15/07 — 5.000.000
        db.AccountingEntries.Add(CoreAccountingEntry.CreateExpense(
            tenantId, period, new Money(5_000_000m, "VND"), "Mua hàng 4",
            accountCode: "642", vendor: "Cty XYZ", transactionDate: new DateTime(2026, 7, 15)));
        _ = await db.SaveChangesAsync();

        PaymentComplianceService service = CreateService(db);

        // Act
        CashPaymentReviewDto review = await service.GetCashPaymentReviewAsync(tenantId, 2026, 7);

        // Assert — chỉ 1 nhóm gộp: Cty ABC ngày 15/07 tổng 24tr, 2 phiếu
        SameDayAggregateDto aggregate = Assert.Single(review.SameDayAggregates);
        Assert.Equal("Cty ABC", aggregate.Vendor);
        Assert.Equal(new DateTime(2026, 7, 15), aggregate.Date);
        Assert.Equal(24_000_000m, aggregate.TotalAmount);
        Assert.Equal(2, aggregate.EntryCount);
        Assert.Empty(review.Items); // không có order (mock rỗng)
    }

    [Fact]
    public async Task GetCashPaymentReviewAsync_IsTenantIsolated()
    {
        // Arrange — tenant A có nhóm gộp
        TenantId tenantA = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantA);
        VanAnDbContext dbA = _fixture.CreateFreshDbContext();
        dbA.AccountingEntries.Add(CoreAccountingEntry.CreateExpense(
            tenantA, new AccountingPeriod(2026, 7), new Money(21_000_000m, "VND"), "Mua A",
            accountCode: "642", vendor: "Cty A", transactionDate: new DateTime(2026, 7, 10)));
        _ = await dbA.SaveChangesAsync();

        // Act — tenant B không thấy
        TenantId tenantB = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantB);
        VanAnDbContext dbB = _fixture.CreateFreshDbContext();
        PaymentComplianceService service = CreateService(dbB);
        CashPaymentReviewDto review = await service.GetCashPaymentReviewAsync(tenantB, 2026, 7);

        // Assert
        Assert.Empty(review.SameDayAggregates);
        Assert.Empty(review.Items);
    }
}