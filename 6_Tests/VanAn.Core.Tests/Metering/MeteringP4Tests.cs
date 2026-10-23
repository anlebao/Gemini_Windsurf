using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Booking;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Core.Tests.BookingScheduling;
using VanAn.Gateway.Controllers;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using Xunit;
using ShopMeteringClient = VanAn.ShopERP.Services.MeteringApiClient;

namespace VanAn.Core.Tests.Metering;

/// <summary>
/// Phí nền tảng P4 — task_p4 test matrix T01–T09 (T10 is manual/production RV).
/// Covers: fail-safe client (timeout/circuit/disabled), FeatureUsageRecorder key format,
/// chat hooks in both messaging services, booking hook after commit, public endpoint
/// whitelist + bot filter.
/// </summary>
public class MeteringP4Tests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private sealed class CapturingMeteringClient : IMeteringClient
    {
        public List<MeteringUsageEvent> Events { get; } = new();
        public int Calls { get; private set; }

        public Task<bool> RecordUsageAsync(MeteringUsageEvent evt, CancellationToken ct = default)
        {
            Calls++;
            lock (Events) Events.Add(evt);
            return Task.FromResult(true);
        }

        public Task<bool> RecordUsageBatchAsync(IReadOnlyList<MeteringUsageEvent> events, CancellationToken ct = default)
        {
            Calls++;
            lock (Events) Events.AddRange(events);
            return Task.FromResult(true);
        }

        public Task<MeteringBalanceCheck?> CheckBalanceAsync(Guid tenantId, string serviceCode, CancellationToken ct = default)
            => Task.FromResult<MeteringBalanceCheck?>(null);
    }

    private sealed class FakeTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid TenantId { get; } = tenantId;
        public string? CurrentUser => "test";
        public bool HasTenant => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) { /* fixed in ctor */ }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return send(request, cancellationToken);
        }
    }

    private static IConfiguration Config(bool withKeys = true) => new ConfigurationBuilder()
        .AddInMemoryCollection(withKeys
            ? new Dictionary<string, string?> { ["Metering:CallerId"] = "test-caller", ["Metering:CallerKey"] = "key" }
            : new Dictionary<string, string?>())
        .Build();

    // ── T01: timeout → false, never throws ──────────────────────────────

    [Fact]
    public async Task T01_Timeout_ReturnsFalse_NoThrow()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct); // longer than client timeout
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://metering/"), Timeout = TimeSpan.FromMilliseconds(200) };
        var client = new ShopMeteringClient(http, Config(), NullLogger<ShopMeteringClient>.Instance);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ok = await client.RecordUsageAsync(new MeteringUsageEvent(
            Guid.NewGuid(), "chat.message", 1, "message", DateTime.UtcNow, "k1"));

        ok.Should().BeFalse();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "client must not block business code");
    }

    // ── T02: circuit breaker opens after 5 failures, short-circuits ─────

    [Fact]
    public async Task T02_CircuitBreaker_OpensAfterFailures_ShortCircuits()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("connection refused"));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://metering/") };
        var client = new ShopMeteringClient(http, Config(), NullLogger<ShopMeteringClient>.Instance);
        var evt = new MeteringUsageEvent(Guid.NewGuid(), "chat.message", 1, "message", DateTime.UtcNow, "k1");

        for (var i = 0; i < 5; i++)
            (await client.RecordUsageAsync(evt)).Should().BeFalse();
        handler.Requests.Should().Be(5);

        // Circuit open → short-circuit: NO outbound request.
        (await client.RecordUsageAsync(evt)).Should().BeFalse();
        (await client.RecordUsageAsync(evt)).Should().BeFalse();
        handler.Requests.Should().Be(5, "circuit must short-circuit without outbound calls");
    }

    // ── T03: FeatureUsageRecorder idempotency format ────────────────────

    [Fact]
    public async Task T03_FeatureUsageRecorder_IdempotencyFormat()
    {
        var tenantId = Guid.NewGuid();
        var capture = new CapturingMeteringClient();
        var recorder = new VanAn.ShopERP.Services.FeatureUsageRecorder(
            capture, new FakeTenantProvider(tenantId), NullLogger<VanAn.ShopERP.Services.FeatureUsageRecorder>.Instance);

        await recorder.RecordAsync("feature.financial");
        // Fire-and-forget — the capture happens synchronously inside the recording call.
        await Task.Delay(50);

        var evt = capture.Events.Should().ContainSingle().Subject;
        evt.ServiceCode.Should().Be("feature.financial");
        evt.TenantId.Should().Be(tenantId);
        evt.Quantity.Should().Be(1);
        evt.Unit.Should().Be("module-month");
        evt.IdempotencyKey.Should().Be(
            $"feature:feature.financial:{tenantId}:{DateTime.UtcNow:yyyy-MM}");
    }

    // ── T04: RealtimeMessagingService — chat.message + chat.bytes ──────

    [Fact]
    public async Task T04_RealtimeMessaging_SendsUsage_WithTenantAndBytes()
    {
        var (ctx, conn) = NewDb();
        await using var _ = ctx; await using var __ = conn;
        var metering = new CapturingMeteringClient();
        var svc = new RealtimeMessagingService(ctx, NullLogger<RealtimeMessagingService>.Instance, metering: metering);

        var tenant = new TenantId(Guid.NewGuid());
        var customer = Guid.NewGuid(); var shop = Guid.NewGuid();
        var conversation = await svc.EnsureConversationAsync(
            tenant, RealtimeSubjectType.Shop, Guid.NewGuid(), customer, shop);
        conversation.Should().NotBeNull();

        var content = "xin chào 👋"; // multi-byte — exercises UTF8 byte count
        var message = await svc.SendMessageAsync(conversation!.Id, customer, content);
        message.Should().NotBeNull();

        await Task.Delay(100); // fire-and-forget dispatch
        metering.Events.Should().HaveCount(2);
        var msg = metering.Events.Single(e => e.ServiceCode == "chat.message");
        var bytes = metering.Events.Single(e => e.ServiceCode == "chat.bytes");
        msg.TenantId.Should().Be(tenant.Value);
        msg.Quantity.Should().Be(1);
        msg.IdempotencyKey.Should().Be($"msg:{message!.Id}");
        bytes.Quantity.Should().Be(System.Text.Encoding.UTF8.GetByteCount(content));
        bytes.IdempotencyKey.Should().Be($"msg:{message.Id}:bytes");
    }

    // ── T05: ChatService — same contract ────────────────────────────────

    [Fact]
    public async Task T05_ChatService_SendsUsage_WithTenantAndBytes()
    {
        var (ctx, conn) = NewDb();
        await using var _ = ctx; await using var __ = conn;
        var metering = new CapturingMeteringClient();
        var svc = new ChatService(ctx, NullLogger<ChatService>.Instance, metering: metering);

        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        // FK: Order.CustomerId → Customers.Id — seed customer first (ChatServiceTests pattern).
        var customer = new Customer(
            new TenantId(tenantId), "Test Customer", "0901234567");
        SetProp(customer, "Id", customerId);
        ctx.Customers.Add(customer);

        var orderId = Guid.NewGuid();
        var order = new Order(new TenantId(tenantId), null, 0);
        SetProp(order, "Id", orderId);
        SetProp(order, "OrderId", new OrderId(orderId));
        SetProp(order, "OrderType", "DELIVERY");
        SetProp(order, "Status", new OrderStatusId("confirmed"));
        SetProp(order, "CustomerId", customerId);
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var content = "hello 123";
        var message = await svc.SendMessageAsync(orderId, customerId, content);
        message.Should().NotBeNull();

        await Task.Delay(100);
        metering.Events.Should().HaveCount(2);
        metering.Events.Single(e => e.ServiceCode == "chat.message").Quantity.Should().Be(1);
        var bytes = metering.Events.Single(e => e.ServiceCode == "chat.bytes");
        bytes.Quantity.Should().Be(System.Text.Encoding.UTF8.GetByteCount(content));
        bytes.TenantId.Should().Be(tenantId);
    }

    // ── T06: BookingService — feature.booking after commit ─────────────

    [Fact]
    public async Task T06_BookingService_RecordsFeatureBooking_AfterSave()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
        var ctx = scope.Context;
        var metering = new CapturingMeteringClient();
        var svc = new BookingService(ctx,
            new AvailabilityService(ctx, NullLogger<AvailabilityService>.Instance),
            NullLogger<BookingService>.Instance,
            metering: metering);
        await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
        var (_, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

        var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
            new CreateBookingCommand(offeringId, BookingTestData.At(14, 0), null, null, "dev", null, []),
            "metering-key-1");

        booking.Should().NotBeNull();
        await Task.Delay(100);
        var evt = metering.Events.Should().ContainSingle().Subject;
        evt.ServiceCode.Should().Be("feature.booking");
        evt.TenantId.Should().Be(BookingTestData.TenantId.Value);
        evt.Quantity.Should().Be(1);
        evt.Unit.Should().Be("booking");
        evt.IdempotencyKey.Should().Be($"booking:{booking.Id}");
    }

    // ── T07: bot UA → 200 accepted:false, no forward ────────────────────

    [Fact]
    public void T07_PublicMetering_BotUserAgent_NotForwarded()
    {
        var metering = new CapturingMeteringClient();
        var ctrl = NewController(metering, userAgent: "curl/7.88");
        var req = new PublicMeteringController.PublicMeteringEventRequest(
            Guid.NewGuid().ToString(), "store.visit", 1, "visit", DateTime.UtcNow, "visit:x:y:z");

        var result = ctrl.RecordEvent(req).Should().BeOfType<OkObjectResult>().Subject;
        metering.Calls.Should().Be(0, "bot traffic must not be forwarded to metering");
        result.Value.Should().BeEquivalentTo(new { accepted = false });
    }

    // ── T08: non-whitelisted serviceCode → 400 ──────────────────────────

    [Fact]
    public void T08_PublicMetering_NonWhitelistedServiceCode_BadRequest()
    {
        var metering = new CapturingMeteringClient();
        var ctrl = NewController(metering, userAgent: "Mozilla/5.0");
        var req = new PublicMeteringController.PublicMeteringEventRequest(
            Guid.NewGuid().ToString(), "feature.booking", 1, "booking", DateTime.UtcNow, "k");

        ctrl.RecordEvent(req).Should().BeOfType<BadRequestObjectResult>();
        metering.Calls.Should().Be(0);
    }

    // ── T09: missing config → disabled, no-op ───────────────────────────

    [Fact]
    public async Task T09_MissingConfig_DisablesClient_NoOp()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://metering/") };
        var client = new ShopMeteringClient(http, Config(withKeys: false), NullLogger<ShopMeteringClient>.Instance);

        (await client.RecordUsageAsync(new MeteringUsageEvent(
            Guid.NewGuid(), "store.visit", 1, "visit", DateTime.UtcNow, "k"))).Should().BeFalse();
        handler.Requests.Should().Be(0, "disabled client must never hit the network");

        (await client.CheckBalanceAsync(Guid.NewGuid(), "chat.message")).Should().BeNull();
    }

    // ── Support ──────────────────────────────────────────────────────────

    private static PublicMeteringController NewController(CapturingMeteringClient metering, string userAgent)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.UserAgent = userAgent;
        var ctrl = new PublicMeteringController(metering, NullLogger<PublicMeteringController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };
        return ctrl;
    }

    private static (VanAnDbContext, SqliteConnection) NewDb()
    {
        var conn = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        conn.Open();
        var efProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseInternalServiceProvider(efProvider).UseSqlite(conn)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new VanAnDbContext(options);
        ctx.Database.EnsureCreated();
        return (ctx, conn);
    }

    private static void SetProp(object entity, string name, object value)
    {
        var prop = entity.GetType().GetProperty(name);
        if (prop is null) return;
        var setter = prop.SetMethod ?? prop.GetSetMethod(nonPublic: true);
        setter?.Invoke(entity, [value]);
    }
}
