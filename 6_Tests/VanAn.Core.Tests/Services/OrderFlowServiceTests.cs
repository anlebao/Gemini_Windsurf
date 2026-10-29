using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Issue #195 (2026-10-19): tests cho OrderFlowService — resolution precedence
/// (tenant+source → tenant → system+source → system → builtin), tenant isolation,
/// upsert validation, booking reduced flow, và OrderStatusHistory writes.
/// </summary>
public class OrderFlowServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _sp;
    private readonly VanAnDbContext _db;
    private readonly OrderFlowService _flow;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    public OrderFlowServiceTests()
    {
        _connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        _connection.Open();

        var services = new ServiceCollection();
        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        services.AddDbContext<VanAnDbContext>(options =>
        {
            options.UseInternalServiceProvider(efServiceProvider);
            options.UseSqlite(_connection);
            options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        });
        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        services.AddMemoryCache();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        var tenantProvider = new TestTenantProvider();
        tenantProvider.SetTenant(_tenantA);
        services.AddSingleton<VanAn.Shared.Domain.Common.ITenantProvider>(tenantProvider);

        _sp = services.BuildServiceProvider();
        _db = _sp.GetRequiredService<VanAnDbContext>();
        _ = _db.Database.EnsureCreated();
        _flow = new OrderFlowService(
            _db,
            _sp.GetRequiredService<IMemoryCache>(),
            NullLogger<OrderFlowService>.Instance);
    }

    public void Dispose()
    {
        _sp.Dispose();
        _connection.Dispose();
    }

    private static List<OrderFlowStepInput> KitchenSteps() =>
    [
        new("pending", "Chờ xác nhận", 1, false, ["confirmed", "cancelled"]),
        new("confirmed", "Đã xác nhận", 2, false, ["completed", "cancelled"]),
        new("completed", "Hoàn thành", 3, true, []),
        new("cancelled", "Đã hủy", 4, true, [])
    ];

    // ── T1: builtin fallback khi chưa có profile nào ───────────────────────
    [Fact(DisplayName = "OF-T1: Resolve — no profiles → builtin kitchen flow")]
    public async Task Resolve_NoProfiles_ReturnsBuiltin()
    {
        OrderFlowResolution flow = await _flow.ResolveFlowAsync(_tenantA, null);

        Assert.Equal("builtin", flow.Source);
        Assert.Null(flow.ProfileId);
        Assert.True(flow.IsAllowed("pending", "confirmed"));
        Assert.True(flow.IsAllowed("confirmed", "preparing"));
    }

    // ── T2: booking builtin = reduced flow (issue 0.2) ─────────────────────
    [Fact(DisplayName = "OF-T2: Resolve — booking → builtin reduced flow (pay+complete only)")]
    public async Task Resolve_Booking_ReturnsReducedFlow()
    {
        OrderFlowResolution flow = await _flow.ResolveFlowAsync(_tenantA, "booking");

        Assert.Equal("builtin", flow.Source);
        Assert.True(flow.IsAllowed("pending", "confirmed"));   // "Thanh toán"
        Assert.True(flow.IsAllowed("confirmed", "completed")); // "Hoàn thành"
        Assert.False(flow.IsAllowed("pending", "preparing"));  // booking KHÔNG có bước bếp
        Assert.False(flow.IsAllowed("confirmed", "ready"));
        Assert.False(flow.IsAllowed("pending", "delivered"));
    }

    // ── T3: EnsureSystemDefaults seed 2 profiles, idempotent ───────────────
    [Fact(DisplayName = "OF-T3: EnsureSystemDefaults — seeds system kitchen + booking, idempotent")]
    public async Task EnsureSystemDefaults_SeedsOnce()
    {
        await _flow.EnsureSystemDefaultsAsync();
        List<OrderFlowProfileDto> profiles = await _flow.GetProfilesAsync(Guid.Empty);

        Assert.Equal(2, profiles.Count);
        Assert.Contains(profiles, p => p.SourceType == null);
        Assert.Contains(profiles, p => p.SourceType == "booking");

        // Idempotent — chạy lần 2 không tạo thêm
        await _flow.EnsureSystemDefaultsAsync();
        profiles = await _flow.GetProfilesAsync(Guid.Empty);
        Assert.Equal(2, profiles.Count);
    }

    // ── T4: tenant profile beats system ────────────────────────────────────
    [Fact(DisplayName = "OF-T4: Resolve — tenant profile beats system profile")]
    public async Task Resolve_TenantProfile_BeatsSystem()
    {
        await _flow.EnsureSystemDefaultsAsync();
        OrderFlowProfileDto tenantProfile = await _flow.UpsertProfileAsync(
            _tenantA, null, null, "Luồng riêng tenant A", KitchenSteps(), "test");

        OrderFlowResolution flow = await _flow.ResolveFlowAsync(_tenantA, null);

        Assert.Equal("database", flow.Source);
        Assert.Equal(tenantProfile.Id, flow.ProfileId);
        Assert.Equal("Luồng riêng tenant A", flow.ProfileName);
        // Tenant flow rút gọn: confirmed → completed (không qua preparing)
        Assert.True(flow.IsAllowed("confirmed", "completed"));
        Assert.False(flow.IsAllowed("confirmed", "preparing"));
    }

    // ── T5: source-specific profile beats wildcard ─────────────────────────
    [Fact(DisplayName = "OF-T5: Resolve — tenant+booking profile beats tenant wildcard + system")]
    public async Task Resolve_SourceMatch_BeatsWildcard()
    {
        await _flow.EnsureSystemDefaultsAsync();
        // Tenant wildcard profile (mọi nguồn)
        await _flow.UpsertProfileAsync(_tenantA, null, null, "Tenant A wildcard", KitchenSteps(), "test");
        // Tenant booking-specific profile
        OrderFlowProfileDto bookingProfile = await _flow.UpsertProfileAsync(
            _tenantA, "booking", null, "Tenant A booking",
            [
                new("pending", "Chờ TT", 1, false, ["confirmed", "cancelled"]),
                new("confirmed", "Đã TT", 2, false, ["completed", "cancelled"]),
                new("completed", "Hoàn thành", 3, true, []),
                new("cancelled", "Đã hủy", 4, true, [])
            ], "test");

        OrderFlowResolution bookingFlow = await _flow.ResolveFlowAsync(_tenantA, "booking");
        Assert.Equal(bookingProfile.Id, bookingFlow.ProfileId);

        OrderFlowResolution posFlow = await _flow.ResolveFlowAsync(_tenantA, "pos");
        Assert.NotEqual(bookingProfile.Id, posFlow.ProfileId); // pos → wildcard tenant profile
    }

    // ── T6: tenant isolation — B resolve không bao giờ thấy profile của A ──
    [Fact(DisplayName = "OF-T6: Resolve — tenant isolation (B không thấy profile A)")]
    public async Task Resolve_TenantIsolation()
    {
        OrderFlowProfileDto profileA = await _flow.UpsertProfileAsync(
            _tenantA, null, null, "Tenant A only", KitchenSteps(), "test");

        OrderFlowResolution flowB = await _flow.ResolveFlowAsync(_tenantB, null);

        Assert.NotEqual(profileA.Id, flowB.ProfileId);
        Assert.NotEqual("Tenant A only", flowB.ProfileName);
        // B không có profile → builtin fallback (system chưa seed trong test này)
        Assert.Equal("builtin", flowB.Source);
    }

    // ── T7: upsert validation ──────────────────────────────────────────────
    [Fact(DisplayName = "OF-T7: Upsert — validation (pending/terminal/dup/unknown-next)")]
    public async Task Upsert_ValidatesSteps()
    {
        // Thiếu 'pending'
        await Assert.ThrowsAsync<ArgumentException>(() => _flow.UpsertProfileAsync(
            _tenantA, null, null, "Bad", [new("confirmed", "X", 1, true, [])], "test"));

        // Không có terminal step
        await Assert.ThrowsAsync<ArgumentException>(() => _flow.UpsertProfileAsync(
            _tenantA, null, null, "Bad",
            [new("pending", "P", 1, false, ["confirmed"]), new("confirmed", "C", 2, false, ["pending"])], "test"));

        // Duplicate codes
        await Assert.ThrowsAsync<ArgumentException>(() => _flow.UpsertProfileAsync(
            _tenantA, null, null, "Bad",
            [new("pending", "P", 1, false, []), new("pending", "P2", 2, true, [])], "test"));

        // Unknown next status
        await Assert.ThrowsAsync<ArgumentException>(() => _flow.UpsertProfileAsync(
            _tenantA, null, null, "Bad",
            [new("pending", "P", 1, false, ["ghost"]), new("cancelled", "C", 2, true, [])], "test"));

        // #195 hardening: thiếu anchor 'completed' (loyalty/accounting side-effect)
        await Assert.ThrowsAsync<ArgumentException>(() => _flow.UpsertProfileAsync(
            _tenantA, null, null, "Bad",
            [new("pending", "P", 1, false, ["confirmed", "cancelled"]),
             new("confirmed", "C", 2, false, ["cancelled"]),
             new("cancelled", "X", 3, true, [])], "test"));

        // #195 hardening: 'cancelled' không terminal (absorbing state bắt buộc)
        await Assert.ThrowsAsync<ArgumentException>(() => _flow.UpsertProfileAsync(
            _tenantA, null, null, "Bad",
            [new("pending", "P", 1, false, ["cancelled", "completed"]),
             new("cancelled", "X", 2, false, ["completed"]),
             new("completed", "Done", 3, true, [])], "test"));

        // #195 follow-up (2026-10-28 — incident Cafe Tân Quy): step KHÔNG terminal có
        // AllowedNextCodes rỗng → đơn kẹt vĩnh viễn → reject.
        await Assert.ThrowsAsync<ArgumentException>(() => _flow.UpsertProfileAsync(
            _tenantA, null, null, "Bad",
            [new("pending", "P", 1, false, []),
             new("completed", "Done", 2, true, []),
             new("cancelled", "Hủy", 3, true, [])], "test"));

        // Terminal steps vẫn OK với AllowedNextCodes rỗng (absorbing state).
        OrderFlowProfileDto terminalOk = await _flow.UpsertProfileAsync(
            _tenantA, null, null, "TerminalOk",
            [new("pending", "P", 1, false, ["completed", "cancelled"]),
             new("completed", "Done", 2, true, []),
             new("cancelled", "Hủy", 3, true, [])], "test");
        Assert.Equal(3, terminalOk.Steps.Count);

        // Valid → OK
        OrderFlowProfileDto ok = await _flow.UpsertProfileAsync(
            _tenantA, null, null, "Good", KitchenSteps(), "test");
        Assert.Equal(4, ok.Steps.Count);
    }

    // ── T8: upsert thay thế steps (không duplicate) ────────────────────────
    [Fact(DisplayName = "OF-T8: Upsert — same key replaces steps (upsert semantics)")]
    public async Task Upsert_SameKey_ReplacesSteps()
    {
        await _flow.UpsertProfileAsync(_tenantA, null, null, "V1", KitchenSteps(), "test");
        OrderFlowProfileDto v2 = await _flow.UpsertProfileAsync(
            _tenantA, null, null, "V2",
            [
                new("pending", "P", 1, false, ["completed", "cancelled"]),
                new("completed", "Done", 2, true, []),
                new("cancelled", "Hủy", 3, true, [])
            ], "test");

        Assert.Equal("V2", v2.Name);
        Assert.Equal(3, v2.Steps.Count);

        List<OrderFlowProfileDto> all = await _flow.GetProfilesAsync(_tenantA);
        Assert.Single(all.Where(p => p.IsActive));
    }

    // ── T9: deactivate → rơi về fallback ───────────────────────────────────
    [Fact(DisplayName = "OF-T9: Deactivate — profile removed from resolution")]
    public async Task Deactivate_FallsBack()
    {
        OrderFlowProfileDto p = await _flow.UpsertProfileAsync(
            _tenantA, null, null, "Custom", KitchenSteps(), "test");

        OrderFlowResolution before = await _flow.ResolveFlowAsync(_tenantA, null);
        Assert.Equal(p.Id, before.ProfileId);

        await _flow.DeactivateProfileAsync(p.Id, "test");

        OrderFlowResolution after = await _flow.ResolveFlowAsync(_tenantA, null);
        Assert.Equal("builtin", after.Source);
    }

    // ── T10: GetNextStatuses qua interface ─────────────────────────────────
    [Fact(DisplayName = "OF-T10: GetNextStatuses — booking pending → chỉ confirmed+cancelled")]
    public async Task GetNextStatuses_Booking()
    {
        IReadOnlyList<string> next = await _flow.GetNextStatusesAsync(_tenantA, "booking", "pending");
        Assert.Contains("confirmed", next);
        Assert.Contains("cancelled", next);
        Assert.DoesNotContain("preparing", next);
        Assert.DoesNotContain("ready", next);
    }
}

/// <summary>
/// Issue #195: OrderWorkflowService — dynamic flow validation + OrderStatusHistory.
/// Booking order pending→preparing bị reject (booking flow không có bước bếp);
/// mọi transition hợp lệ ghi history row cùng transaction.
/// </summary>
public class OrderFlowWorkflowTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _sp;
    private readonly VanAnDbContext _db;
    private readonly IOrderWorkflowService _workflow;
    private readonly Mock<IOrderService> _orderServiceMock;
    private readonly Guid _tenantId = Guid.NewGuid();

    public OrderFlowWorkflowTests()
    {
        _connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        _connection.Open();

        var services = new ServiceCollection();
        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        services.AddDbContext<VanAnDbContext>(options =>
        {
            options.UseInternalServiceProvider(efServiceProvider);
            options.UseSqlite(_connection);
            options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        });
        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        services.AddMemoryCache();

        var socialCampaignMock = new Mock<ISocialCampaignService>();
        socialCampaignMock.Setup(s => s.GetCampaignByTrackingCodeAsync(It.IsAny<string>()))
            .ReturnsAsync((SocialCampaign?)null);
        services.AddSingleton(socialCampaignMock.Object);
        services.AddScoped<ILoyaltyRewardsService, LoyaltyRewardsService>();
        services.AddScoped<VanAn.CoreHub.Repositories.ILoyaltyRewardsRepository, VanAn.CoreHub.Infrastructure.Repositories.LoyaltyRewardsRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        var natsMock = new Mock<INatsEventPublisher>();
        natsMock.SetupGet(n => n.IsConnected).Returns(false);
        services.AddSingleton(natsMock.Object);
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        var tenantProvider = new TestTenantProvider();
        tenantProvider.SetTenant(_tenantId);
        services.AddSingleton<VanAn.Shared.Domain.Common.ITenantProvider>(tenantProvider);

        // Issue #195: real OrderFlowService (EF → test SQLite) — dynamic transitions active
        services.AddScoped<VanAn.Shared.Services.IOrderFlowService>(sp => new OrderFlowService(
            sp.GetRequiredService<IVanAnDbContext>(),
            sp.GetRequiredService<IMemoryCache>(),
            NullLogger<OrderFlowService>.Instance));
        // Issue #195 follow-up (2026-10-29, user approve A): mock IOrderService — booking
        // "Thanh toán" (pending→confirmed) phải kích hoạt ConfirmPaymentAsync (payment thật)
        // TRƯỚC khi chuyển status. Callback mô phỏng MarkPaidAsync (cập nhật PaymentStatus).
        var orderServiceMock = new Mock<IOrderService>();
        orderServiceMock
            .Setup(s => s.ConfirmPaymentAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid orderId, Guid tenantId, string txId, CancellationToken ct) =>
            {
                Order? o = await _db.Orders.FirstOrDefaultAsync(x => x.Id == orderId, ct);
                if (o != null && o.PaymentStatus != "Paid")
                {
                    o.ConfirmPayment(txId, o.PaymentMethod ?? "Cash");
                    await _db.SaveChangesAsync(ct);
                }
            });
        services.AddSingleton(orderServiceMock.Object);
        _orderServiceMock = orderServiceMock;
        services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();

        _sp = services.BuildServiceProvider();
        _db = _sp.GetRequiredService<VanAnDbContext>();
        _ = _db.Database.EnsureCreated();
        _workflow = _sp.GetRequiredService<IOrderWorkflowService>();
    }

    public void Dispose()
    {
        _sp.Dispose();
        _connection.Dispose();
    }

    private async Task<Order> SeedPendingOrderAsync(string? sourceType = null)
    {
        var tid = new TenantId(_tenantId);
        var product = new Product(tid, "P", "D", 50000m, "Cat");
        await _db.Products.AddAsync(product);
        var orderItem = new OrderItem(tid, Guid.Empty, product.Id, 1, 50000m, "P", 0.10m);
        var order = Order.Create(Guid.NewGuid(), tid, null, new List<OrderItem> { orderItem });
        if (sourceType != null) order.SetSourceType(sourceType);
        await _db.Orders.AddAsync(order);
        await _db.SaveChangesAsync();
        return order;
    }

    // ── W1: transition hợp lệ → ghi OrderStatusHistory ─────────────────────
    [Fact(DisplayName = "OF-W1: Transition pending→confirmed writes OrderStatusHistory")]
    public async Task Transition_WritesStatusHistory()
    {
        Order order = await SeedPendingOrderAsync();

        Order? result = await _workflow.TransitionStatusAsync(
            order.Id, new OrderStatusId("confirmed"), reason: "test", changedBy: "owner@test");

        Assert.NotNull(result);
        Assert.Equal("confirmed", result.Status.Value);

        OrderStatusHistory? history = await _db.OrderStatusHistories
            .FirstOrDefaultAsync(h => h.OrderId == order.Id);
        Assert.NotNull(history);
        Assert.Equal("pending", history.FromStatus);
        Assert.Equal("confirmed", history.ToStatus);
        Assert.Equal("owner@test", history.ChangedBy);
        Assert.Equal("test", history.Reason);
        Assert.Equal(order.TenantId.Value, history.TenantId.Value);
    }

    // ── W2: booking order — pending→preparing REJECTED (flow rút gọn 0.2) ──
    [Fact(DisplayName = "OF-W2: Booking order pending→preparing rejected (no kitchen steps)")]
    public async Task Transition_BookingRejectsKitchenStep()
    {
        Order order = await SeedPendingOrderAsync("booking");

        Order? result = await _workflow.TransitionStatusAsync(
            order.Id, new OrderStatusId("preparing"));

        Assert.Null(result); // invalid transition → null

        // Order vẫn pending + KHÔNG có history row
        Order reloaded = await _db.Orders.FirstAsync(o => o.Id == order.Id);
        Assert.Equal("pending", reloaded.Status.Value);
        Assert.Empty(await _db.OrderStatusHistories.Where(h => h.OrderId == order.Id).ToListAsync());
    }

    // ── W3: booking order pending→confirmed (Thanh toán) → OK + history ────
    [Fact(DisplayName = "OF-W3: Booking order pending→confirmed→completed (pay + complete only)")]
    public async Task Transition_BookingPayAndComplete()
    {
        Order order = await SeedPendingOrderAsync("booking");

        Order? paid = await _workflow.TransitionStatusAsync(order.Id, new OrderStatusId("confirmed"));
        Assert.NotNull(paid);
        Assert.Equal("confirmed", paid.Status.Value);
        // Issue #195 follow-up: "Thanh toán" = payment THẬT — PaymentStatus phải Paid
        Assert.Equal("Paid", paid.PaymentStatus);

        Order? done = await _workflow.TransitionStatusAsync(order.Id, new OrderStatusId("completed"));
        Assert.NotNull(done);
        Assert.Equal("completed", done.Status.Value);

        List<OrderStatusHistory> history = await _db.OrderStatusHistories
            .Where(h => h.OrderId == order.Id)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal("pending", history[0].FromStatus);
        Assert.Equal("confirmed", history[0].ToStatus);
        Assert.Equal("confirmed", history[1].FromStatus);
        Assert.Equal("completed", history[1].ToStatus);
    }

    // ── W5: booking "Thanh toán" kích hoạt ConfirmPaymentAsync — đơn thường thì KHÔNG ──
    [Fact(DisplayName = "OF-W5: Booking confirmed triggers ConfirmPaymentAsync (payment) — normal order does not")]
    public async Task Transition_BookingConfirmed_PaysFirst_NormalDoesNot()
    {
        _orderServiceMock.Invocations.Clear();

        // Booking order → pending→confirmed phải gọi ConfirmPaymentAsync (payment thật)
        Order bookingOrder = await SeedPendingOrderAsync("booking");
        Order? paid = await _workflow.TransitionStatusAsync(bookingOrder.Id, new OrderStatusId("confirmed"));
        Assert.NotNull(paid);
        _orderServiceMock.Verify(s => s.ConfirmPaymentAsync(
            bookingOrder.Id, _tenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        // Normal order → confirmed KHÔNG gọi payment (chỉ là xác nhận của staff)
        Order normalOrder = await SeedPendingOrderAsync();
        Order? confirmed = await _workflow.TransitionStatusAsync(normalOrder.Id, new OrderStatusId("confirmed"));
        Assert.NotNull(confirmed);
        _orderServiceMock.Verify(s => s.ConfirmPaymentAsync(
            normalOrder.Id, _tenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── W4: normal order pending→preparing allowed (kitchen builtin) ───────
    [Fact(DisplayName = "OF-W4: Normal order pending→preparing allowed (kitchen flow)")]
    public async Task Transition_NormalOrderKitchenStep()
    {
        Order order = await SeedPendingOrderAsync();

        Order? result = await _workflow.TransitionStatusAsync(order.Id, new OrderStatusId("preparing"));

        Assert.NotNull(result);
        Assert.Equal("preparing", result.Status.Value);
    }
}
