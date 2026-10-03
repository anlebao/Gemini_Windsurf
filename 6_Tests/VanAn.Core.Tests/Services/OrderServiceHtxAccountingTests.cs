using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Interfaces;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using TenantAggregate = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// TT 71 (Phase 6 — 2026-10-02): OrderService — tag nội bộ/ngoài tại CreateOrderAsync
/// + định khoản theo standard (HTX: 512/612 nội vs 511/611 ngoài; DN: 511/632 không regress).
/// </summary>
public class OrderServiceHtxAccountingTests
{
    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly Mock<IAccountingService> _accounting = new();
    private readonly Mock<IHKDBookRepository> _hkdBook = new();
    private readonly Mock<IAccountingEntryRepository> _entryRepo = new();

    private OrderService BuildService(VanAnDbContext db) => new(
        _orderRepo.Object, _accounting.Object, _hkdBook.Object, _entryRepo.Object,
        NullLogger<OrderService>.Instance, dbContext: db);

    private static Order BuildOrder(Guid customerId)
    {
        var order = Order.Create(Guid.NewGuid(), new TenantId(Guid.NewGuid()), customerId, []);
        // Empty items → set amounts via reflection (fallback COGS 70% TotalPrice).
        typeof(Order).GetProperty("SubTotal")!.SetValue(order, 100m);
        typeof(Order).GetProperty("TotalVatAmount")!.SetValue(order, 0m);
        typeof(Order).GetProperty("TotalAmount")!.SetValue(order, 100m);
        return order;
    }

    private async Task<(TestContextScope scope, VanAnDbContext db, Guid htxTenantId, Guid memberTenantId, Guid memberOwnerCustomerId)>
        SeedHtxWithMemberAsync()
    {
        TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;

        var htxId = new TenantId(Guid.NewGuid());
        var htx = TenantAggregate.CreateCompany(htxId, "HTX Bán Lẻ C");
        htx.MarkAsHtx(); // Type=HTX + AccountingStandard=TT71
        db.Tenants.Add(htx);
        db.HtxProfiles.Add(HtxProfile.Create(htxId, "v1.0", "v1.0"));

        var memberTenantId = new TenantId(Guid.NewGuid());
        var memberOwnerCustomerId = Guid.NewGuid();
        var memberTenant = TenantAggregate.CreateCompany(memberTenantId, "HKD Thành Viên C");
        memberTenant.AssignOwnerCustomer(memberOwnerCustomerId);
        db.Tenants.Add(memberTenant);
        db.Members.Add(Member.CreateActive(htxId, "MEM-htx-c-000001", MembershipType.OfficialMember,
            memberTenantId: memberTenantId, capitalContributionAmount: 1_000_000m));

        await db.SaveChangesAsync();
        return (scope, db, htxId.Value, memberTenantId.Value, memberOwnerCustomerId);
    }

    // ── Tag nội bộ/ngoài tại CreateOrderAsync ─────────────────────────────

    [Fact]
    public async Task CreateOrderAsync_HtxBuyerIsMember_MarksInternal()
    {
        var (scope, db, htxTenantId, _, memberOwnerCustomerId) = await SeedHtxWithMemberAsync();
        _orderRepo.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync((Order o, CancellationToken _) => o);
        var svc = BuildService(db);

        var order = BuildOrder(memberOwnerCustomerId);
        // Order.Create tự tạo TenantId mới — gán đúng tenant bán (HTX)
        typeof(Order).GetProperty("TenantId")!.SetValue(order, new TenantId(htxTenantId));

        var created = await svc.CreateOrderAsync(order, htxTenantId);
        Assert.True(created.IsInternalToHtx);
    }

    [Fact]
    public async Task CreateOrderAsync_HtxBuyerNotMember_MarksExternal()
    {
        var (scope, db, htxTenantId, _, _) = await SeedHtxWithMemberAsync();
        _orderRepo.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync((Order o, CancellationToken _) => o);
        var svc = BuildService(db);

        var order = BuildOrder(Guid.NewGuid()); // buyer không phải member
        typeof(Order).GetProperty("TenantId")!.SetValue(order, new TenantId(htxTenantId));

        var created = await svc.CreateOrderAsync(order, htxTenantId);
        Assert.False(created.IsInternalToHtx);
    }

    [Fact]
    public async Task CreateOrderFromCommandAsync_HtxCheckout_MarksInternal()
    {
        // RV fix 2026-10-03: path đặt hàng thật (checkout) phải tag IsInternalToHtx
        // (trước đây chỉ CreateOrderAsync — checkout luôn null → internal không định khoản 512/612).
        var (scope, db, htxTenantId, _, memberOwnerCustomerId) = await SeedHtxWithMemberAsync();
        // Bug 1a fix (checkout): CustomerId phải tồn tại trong DB — tenant = ActiveTenantId (TestTenantProvider filter)
        var customer = new Customer(new TenantId(scope.ActiveTenantId), "RV Internal Buyer", "0900000002", "rv@vanan.vn");
        typeof(VanAn.Shared.Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(customer, memberOwnerCustomerId);
        typeof(Customer).GetProperty("CustomerId")!.SetValue(customer, new CustomerId(memberOwnerCustomerId));
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        _orderRepo.Setup(r => r.AddAsyncNoSave(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync((Order o, CancellationToken _) => o);
        _orderRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var tx = new Moq.Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>();
        tx.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _orderRepo.Setup(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        var svc = BuildService(db);

        var command = new VanAn.CoreHub.Commands.CreateOrderCommand
        {
            CustomerId = memberOwnerCustomerId,
            Items =
            [
                new VanAn.CoreHub.Commands.OrderItemRequest
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "RV HTX Internal",
                    Quantity = 1,
                    UnitPrice = 200000m,
                    VatRate = 0.10m
                }
            ]
        };

        var created = await svc.CreateOrderFromCommandAsync(command, htxTenantId);
        Assert.True(created.IsInternalToHtx);
        Assert.Equal(memberOwnerCustomerId, created.CustomerId);
    }

    [Fact]
    public async Task CreateOrderAsync_NonHtxTenant_FlagNull()
    {
        TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;
        var dnTenantId = new TenantId(Guid.NewGuid());
        db.Tenants.Add(TenantAggregate.CreateCompany(dnTenantId, "Công ty DN"));
        await db.SaveChangesAsync();

        _orderRepo.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync((Order o, CancellationToken _) => o);
        var svc = BuildService(db);

        var order = BuildOrder(Guid.NewGuid());
        typeof(Order).GetProperty("TenantId")!.SetValue(order, dnTenantId);

        var created = await svc.CreateOrderAsync(order, dnTenantId.Value);
        Assert.Null(created.IsInternalToHtx);
    }

    // ── Định khoản theo standard (GenerateAccountingEntriesAsync) ─────────

    private class AccountCapture
    {
        public string? Revenue;
        public string? Cogs;
    }

    private void SetupAccountingCapture(AccountCapture cap)
    {
        _accounting.Setup(a => a.CreateRevenueEntryAsync(
                It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>()))
            .Callback<TenantId, AccountingPeriod, decimal, string, string?, string?, IndustrySector?, DateTime?>(
                (_, _, _, _, ac, _, _, _) => cap.Revenue = ac)
            .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto());
        _accounting.Setup(a => a.CreateExpenseEntryAsync(
                It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>()))
            .Callback<TenantId, AccountingPeriod, decimal, string, string?, string?, string?, string?, IndustrySector?, DateTime?>(
                (_, _, _, _, ac, _, _, _, _, _) => cap.Cogs = ac)
            .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto());
    }

    [Fact]
    public async Task GenerateAccounting_HtxInternal_Revenue512Cogs612()
    {
        var (scope, db, htxTenantId, _, memberOwnerCustomerId) = await SeedHtxWithMemberAsync();
        var svc = BuildService(db);

        var order = BuildOrder(memberOwnerCustomerId);
        typeof(Order).GetProperty("TenantId")!.SetValue(order, new TenantId(htxTenantId));
        order.SetHtxInternalFlag(true);

        var cap = new AccountCapture();
        SetupAccountingCapture(cap);

        await svc.GenerateAccountingEntriesAsync(order, new TenantId(htxTenantId));

        Assert.Equal("512", cap.Revenue); // nội bộ → 512
        Assert.Equal("612", cap.Cogs);    // nội bộ → 612
    }

    [Fact]
    public async Task GenerateAccounting_HtxExternal_Revenue511Cogs611()
    {
        var (scope, db, htxTenantId, _, _) = await SeedHtxWithMemberAsync();
        var svc = BuildService(db);

        var order = BuildOrder(Guid.NewGuid());
        typeof(Order).GetProperty("TenantId")!.SetValue(order, new TenantId(htxTenantId));
        order.SetHtxInternalFlag(false);

        var cap = new AccountCapture();
        SetupAccountingCapture(cap);

        await svc.GenerateAccountingEntriesAsync(order, new TenantId(htxTenantId));

        Assert.Equal("511", cap.Revenue); // bên ngoài → 511
        Assert.Equal("611", cap.Cogs);    // bên ngoài → 611
    }

    [Fact]
    public async Task GenerateAccounting_DnTenant_Keeps511And632()
    {
        TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;
        var dnTenantId = new TenantId(Guid.NewGuid());
        db.Tenants.Add(TenantAggregate.CreateCompany(dnTenantId, "Công ty DN"));
        await db.SaveChangesAsync();
        var svc = BuildService(db);

        var order = BuildOrder(Guid.NewGuid());
        typeof(Order).GetProperty("TenantId")!.SetValue(order, dnTenantId);

        var cap = new AccountCapture();
        SetupAccountingCapture(cap);

        await svc.GenerateAccountingEntriesAsync(order, dnTenantId);

        Assert.Equal("511", cap.Revenue); // DN giữ nguyên (không regress)
        Assert.Equal("632", cap.Cogs);
    }
}
