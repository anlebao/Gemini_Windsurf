using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Accounting;

/// <summary>SRS v1.2 W4/C2 (M8): OrderPayments — record idempotent + per-payment entry allocation.</summary>
public class OrderPaymentsC2Tests
{
    private static readonly TenantId Tenant = new(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));

    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly Mock<IAccountingService> _acct = new();
    private readonly Mock<IHKDBookRepository> _hkd = new();
    private readonly Mock<IAccountingEntryRepository> _entryRepo = new();
    private readonly List<(string Ref, string? Account, decimal Amount, string? Method)> _revenueCalls = [];
    private readonly List<(string Ref, string? Account, decimal Amount)> _expenseCalls = [];

    private OrderService BuildService(VanAn.CoreHub.Infrastructure.IAccountingDbContext? db)
    {
        _acct.Setup(a => a.CreateRevenueEntryAsync(
                It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<TenantId, AccountingPeriod, decimal, string, string?, string?, IndustrySector?, DateTime?, string?, string?>(
                (_, _, amount, _, account, reference, _, _, method, cashAccount) => _revenueCalls.Add((reference!, account, amount, method)))
            .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto());
        _acct.Setup(a => a.CreateExpenseEntryAsync(
                It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<TenantId, AccountingPeriod, decimal, string, string?, string?, string?, string?, IndustrySector?, DateTime?, string?, string?>(
                (_, _, amount, _, account, _, _, reference, _, _, _, _) => _expenseCalls.Add((reference!, account, amount)))
            .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto());
        _acct.Setup(a => a.CreateDepositOffsetEntryAsync(
                It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>()))
            .Callback<TenantId, AccountingPeriod, decimal, string, string?, string?, IndustrySector?, DateTime?>(
                (_, _, amount, _, account, reference, _, _) => _revenueCalls.Add((reference!, account, amount, null)))
            .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto());
        return new OrderService(_orderRepo.Object, _acct.Object, _hkd.Object, _entryRepo.Object,
            NullLogger<OrderService>.Instance, accountingDbContext: db);
    }

    private static VanAn.CoreHub.Tests.TestInfrastructure.TestContextScope NewScope()
    {
        var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        return scope;
    }

    private static Order NewOrder(decimal unitPrice = 100m, int qty = 2, decimal deposit = 0m)
    {
        Guid orderId = Guid.NewGuid();
        OrderItem item = OrderItem.Create(Guid.NewGuid(), Tenant, orderId, Guid.NewGuid(), quantity: qty, unitPrice: unitPrice);
        Order order = Order.Create(orderId, Tenant, null, [item]);
        if (deposit > 0) order.SetDepositPaidAmount(deposit);
        return order;
    }

    [Fact]
    public async Task SinglePayment_Parity_NetVatCogsDeposit()
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);
        Order order = NewOrder(); // SubTotal 200, VAT 20, Total 220

        var payment = await svc.RecordOrderPaymentAsync(order, 220m, "CASH", "TXN-1");
        await svc.GenerateForPaymentAsync(order, payment!, Tenant);

        Assert.Contains(_revenueCalls, c => c.Account == "511" && c.Amount == 200m && c.Ref.EndsWith($"-PMT-{payment!.Id:N}"));
        Assert.Contains(_revenueCalls, c => c.Account == "3331" && c.Amount == 20m);
        Assert.Contains(_expenseCalls, c => c.Account == "632" && c.Ref == $"{order.Id}-COGS");
    }

    [Fact]
    public async Task TwoPayments_VatFirst_ThenNet_CogsOnce()
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);
        Order order = NewOrder(); // Total 220 = net 200 + vat 20

        // Payment 1: 50 → VAT 20 trước + net 30; chưa fulfillment → KHÔNG COGS.
        var p1 = await svc.RecordOrderPaymentAsync(order, 50m, "CASH", "TXN-A");
        await svc.GenerateForPaymentAsync(order, p1!, Tenant);
        Assert.Equal(2, _revenueCalls.Count); // vat 20 + net 30
        Assert.Contains(_revenueCalls, c => c.Account == "3331" && c.Amount == 20m);
        Assert.Contains(_revenueCalls, c => c.Account == "511" && c.Amount == 30m);
        Assert.Empty(_expenseCalls);

        // Payment 2: 170 → net 170 (VAT hết); fulfillment → COGS 1 lần.
        var p2 = await svc.RecordOrderPaymentAsync(order, 170m, "VIETQR", "TXN-B");
        await svc.GenerateForPaymentAsync(order, p2!, Tenant);
        Assert.Contains(_revenueCalls, c => c.Account == "511" && c.Amount == 170m);
        Assert.Single(_expenseCalls);
    }

    [Fact]
    public async Task RecordPayment_Idempotent_BySourceEventId()
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);
        Order order = NewOrder();

        var p1 = await svc.RecordOrderPaymentAsync(order, 50m, "CASH", "TXN-SAME");
        var p2 = await svc.RecordOrderPaymentAsync(order, 50m, "CASH", "TXN-SAME");

        Assert.Equal(p1!.Id, p2!.Id);
        Assert.Equal(1, await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .CountAsync(scope.Context.OrderPayments.Where(p => p.OrderId == order.Id)));
    }

    [Fact]
    public async Task RetrySamePayment_NoDuplicateEntries()
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);
        Order order = NewOrder();
        var payment = await svc.RecordOrderPaymentAsync(order, 220m, "CASH", "TXN-1");

        // Lần 2: repo báo entries đã tồn tại cho paymentRef → skip.
        _entryRepo.Setup(r => r.ExistsByReferenceAsync(Tenant, It.Is<string>(s => s.Contains("PMT-")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        await svc.GenerateForPaymentAsync(order, payment!, Tenant);
        Assert.Empty(_revenueCalls);
    }

    [Fact]
    public async Task DepositOffset_FirstPaymentOnly_AndExcess3387()
    {
        using var scope = NewScope();
        var svc = BuildService(scope.Context);
        Order order = NewOrder(deposit: 50m); // net 200, dep 50, vat 20 → cash owed 170

        var p = await svc.RecordOrderPaymentAsync(order, 200m, "CASH", "TXN-D");
        await svc.GenerateForPaymentAsync(order, p!, Tenant);

        // offset 50 (3387→511) + vat 20 + net 150 + excess 30→3387
        Assert.Contains(_revenueCalls, c => c.Ref.EndsWith("-DEPOSIT-OFFSET") && c.Amount == 50m);
        Assert.Contains(_revenueCalls, c => c.Account == "511" && c.Amount == 150m);
        Assert.Contains(_revenueCalls, c => c.Ref.EndsWith("-ADVANCE") && c.Account == "3387" && c.Amount == 30m);
    }
}
