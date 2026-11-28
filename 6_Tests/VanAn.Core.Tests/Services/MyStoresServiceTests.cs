using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.Gateway.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Services;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// "Cửa hàng của tôi" (2026-11-28): union PG sources — Orders (guest+logged)
/// ∪ LoyaltyRewards (Silo pts/tenant) ∪ AllianceWallet breakdown — joined
/// via ITenantSearchService.GetStoreInfoMapAsync (anti-drift W3-2d batch).
/// Rules: cancelled excluded from count/spent; cancelled-only tenant chỉ hiện
/// khi còn điểm; alliance-wins merge; cap 50; không lộ data device khác.
/// </summary>
public class MyStoresServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _sp;
    private readonly VanAnDbContext _db;
    private readonly Mock<IAllianceWalletService> _wallet = new();
    private readonly Mock<ITenantSearchService> _tenantSearch = new();
    private readonly Guid _deviceId = Guid.NewGuid();

    public MyStoresServiceTests()
    {
        _connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        _connection.Open();

        var services = new ServiceCollection();
        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        services.AddDbContext<VanAnDbContext>(options =>
            options.UseInternalServiceProvider(efServiceProvider).UseSqlite(_connection));
        _sp = services.BuildServiceProvider();
        _db = _sp.GetRequiredService<VanAnDbContext>();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _sp.Dispose();
        _connection.Dispose();
    }

    private MyStoresService Build() => new(_db, _wallet.Object, _tenantSearch.Object,
        NullLogger<MyStoresService>.Instance);

    private Guid SeedTenant(string name, string slug)
    {
        var id = Guid.NewGuid();
        _db.Tenants.Add(Tenant.CreateCompany(new TenantId(id), name,
            TenantSettings.Empty().WithSlug(slug)));
        _db.SaveChanges();
        return id;
    }

    private Order SeedOrder(Guid tenantGuid, string? deviceIdStr = null,
        decimal total = 100_000m, string status = "completed", DateTime? orderDate = null)
    {
        var order = new Order(new TenantId(tenantGuid), null, total);
        order.SetCustomerDeviceId(deviceIdStr ?? _deviceId.ToString());
        order.UpdateOrderDetails(new OrderStatusId(status),
            orderDate ?? DateTime.UtcNow, null, null);
        _db.Orders.Add(order);
        _db.SaveChanges();
        return order;
    }

    private void SeedCustomerWithPoints(Guid tenantGuid, int points, Guid? deviceId = null)
    {
        var customer = new Customer(new TenantId(tenantGuid), "Khách Test", "0901234567");
        customer.UpdateCustomerDetails("Khách Test", "0901234567", null, "standard",
            deviceId ?? _deviceId, isActive: true);
        _db.Customers.Add(customer);
        _db.SaveChanges();
        var rewards = new LoyaltyRewards(new TenantId(tenantGuid), customer.Id);
        rewards.AddPoints(points);
        _db.LoyaltyRewards.Add(rewards);
        _db.SaveChanges();
    }

    private void StubStoreInfo(params (Guid Id, string Name, string? Slug)[] stores)
    {
        var map = stores.ToDictionary(s => s.Id, s => new TenantStoreDto
        {
            Id = s.Id,
            Name = s.Name,
            Slug = s.Slug
        });
        _tenantSearch
            .Setup(t => t.GetStoreInfoMapAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                ids.Where(map.ContainsKey).ToDictionary(id => id, id => map[id]));
    }

    private void StubAllianceWallet(params (Guid TenantId, int Net)[] balances)
    {
        var wallet = new AllianceWallet(_deviceId, null);
        _wallet.Setup(w => w.GetWalletByDeviceIdAsync(_deviceId)).ReturnsAsync(wallet);
        _wallet.Setup(w => w.GetTenantBalancesAsync(wallet.Id))
            .ReturnsAsync(balances.Select(b => new WalletTenantBalance(b.TenantId, b.Net)).ToList());
    }

    // ═══ 1. Rỗng — device chưa từng mua/tích điểm ═══════════════════════════
    [Fact]
    public async Task Empty_Device_Returns_Empty()
    {
        var result = await Build().GetMyStoresAsync(_deviceId);
        Assert.Empty(result);
    }

    // ═══ 2. Chỉ orders → count/spent/lastOrderAt, Mode=Silo, 0 điểm ═════════
    [Fact]
    public async Task Orders_Only_Returns_Order_Stats_Silo_Mode()
    {
        var t1 = SeedTenant("An Tâm", "an-tam");
        SeedOrder(t1, total: 150_000m);
        SeedOrder(t1, total: 50_000m);
        StubStoreInfo((t1, "An Tâm", "an-tam"));

        var result = await Build().GetMyStoresAsync(_deviceId);

        var store = Assert.Single(result);
        Assert.Equal(t1, store.TenantId);
        Assert.Equal("An Tâm", store.Name);
        Assert.Equal("an-tam", store.Slug);
        Assert.Equal(2, store.OrderCount);
        Assert.Equal(200_000m, store.TotalSpent);
        Assert.Equal(0, store.PointBalance);
        Assert.Equal("Silo", store.Mode);
        Assert.NotNull(store.LastOrderAt);
    }

    // ═══ 3. Cancelled loại khỏi count/spent; chỉ-cancelled + 0 điểm → ẩn ════
    [Fact]
    public async Task Cancelled_Excluded_From_Count_And_CancelledOnly_Hidden()
    {
        var t1 = SeedTenant("Có đơn tốt", "ok");
        var t2 = SeedTenant("Chỉ huỷ", "cancel-only");
        SeedOrder(t1, total: 100_000m, status: "completed");
        SeedOrder(t1, total: 999_000m, status: "cancelled");
        SeedOrder(t2, total: 500_000m, status: "cancelled");
        StubStoreInfo((t1, "Có đơn tốt", "ok"), (t2, "Chỉ huỷ", "cancel-only"));

        var result = await Build().GetMyStoresAsync(_deviceId);

        var store = Assert.Single(result);
        Assert.Equal(t1, store.TenantId);
        Assert.Equal(1, store.OrderCount);
        Assert.Equal(100_000m, store.TotalSpent);
    }

    // ═══ 4. Chỉ-cancelled nhưng CÒN ĐIỂM → vẫn hiện (đổi điểm được) ════════
    [Fact]
    public async Task CancelledOnly_Tenant_With_Points_Still_Shown()
    {
        var t1 = SeedTenant("Huỷ hết nhưng còn điểm", "points-left");
        SeedOrder(t1, status: "cancelled");
        SeedCustomerWithPoints(t1, 120);
        StubStoreInfo((t1, "Huỷ hết nhưng còn điểm", "points-left"));

        var result = await Build().GetMyStoresAsync(_deviceId);

        var store = Assert.Single(result);
        Assert.Equal(0, store.OrderCount);
        Assert.Equal(120, store.PointBalance);
    }

    // ═══ 5. Chỉ Silo points (chưa mua) → hiện với OrderCount=0 ═════════════
    [Fact]
    public async Task Silo_Points_Only_Tenant_Shown()
    {
        var t1 = SeedTenant("Tích điểm chưa mua", "silo-only");
        SeedCustomerWithPoints(t1, 75);
        StubStoreInfo((t1, "Tích điểm chưa mua", "silo-only"));

        var store = Assert.Single(await Build().GetMyStoresAsync(_deviceId));
        Assert.Equal(0, store.OrderCount);
        Assert.Equal(75, store.PointBalance);
        Assert.Equal("Silo", store.Mode);
    }

    // ═══ 6. Chỉ Alliance balance → Mode=Alliance ═══════════════════════════
    [Fact]
    public async Task Alliance_Balance_Only_Tenant_Shown_As_Alliance()
    {
        var t1 = SeedTenant("Liên minh", "alliance");
        StubAllianceWallet((t1, 300));
        StubStoreInfo((t1, "Liên minh", "alliance"));

        var store = Assert.Single(await Build().GetMyStoresAsync(_deviceId));
        Assert.Equal(300, store.PointBalance);
        Assert.Equal("Alliance", store.Mode);
    }

    // ═══ 7. Merge: Silo + Alliance cùng tenant → Alliance wins ═════════════
    [Fact]
    public async Task Alliance_Wins_Over_Silo_On_Same_Tenant()
    {
        var t1 = SeedTenant("Cả hai nguồn", "both");
        SeedCustomerWithPoints(t1, 50);
        StubAllianceWallet((t1, 999));
        StubStoreInfo((t1, "Cả hai nguồn", "both"));

        var store = Assert.Single(await Build().GetMyStoresAsync(_deviceId));
        Assert.Equal("Alliance", store.Mode);
        Assert.Equal(999, store.PointBalance);
    }

    // ═══ 8. Privacy — data device khác KHÔNG lộ ════════════════════════════
    [Fact]
    public async Task Other_Device_Data_Not_Leaked()
    {
        var t1 = SeedTenant("Của người khác", "other");
        var otherDevice = Guid.NewGuid();
        SeedOrder(t1, deviceIdStr: otherDevice.ToString());
        SeedCustomerWithPoints(t1, 500, deviceId: otherDevice);
        StubStoreInfo((t1, "Của người khác", "other"));

        var result = await Build().GetMyStoresAsync(_deviceId);
        Assert.Empty(result);
    }

    // ═══ 9. Sort: LastOrderAt mới nhất trước; tenant chỉ-điểm xếp sau ═══════
    [Fact]
    public async Task Sorted_By_LastOrderAt_Desc()
    {
        var tOld = SeedTenant("Cũ", "old");
        var tNew = SeedTenant("Mới", "new");
        var tPoints = SeedTenant("Chỉ điểm", "pts");
        SeedOrder(tOld, orderDate: DateTime.UtcNow.AddDays(-30));
        SeedOrder(tNew, orderDate: DateTime.UtcNow.AddDays(-1));
        SeedCustomerWithPoints(tPoints, 10);
        StubStoreInfo((tOld, "Cũ", "old"), (tNew, "Mới", "new"), (tPoints, "Chỉ điểm", "pts"));

        var result = await Build().GetMyStoresAsync(_deviceId);

        Assert.Equal(new[] { tNew, tOld, tPoints }, result.Select(r => r.TenantId).ToArray());
    }

    // ═══ 10. Cap 50 tenants ════════════════════════════════════════════════
    [Fact]
    public async Task Capped_At_50_Tenants()
    {
        var stores = new List<(Guid, string, string?)>();
        for (var i = 0; i < 55; i++)
        {
            var tid = SeedTenant($"Shop {i:D2}", $"shop-{i:D2}");
            SeedOrder(tid, orderDate: DateTime.UtcNow.AddMinutes(-i));
            stores.Add((tid, $"Shop {i:D2}", $"shop-{i:D2}"));
        }
        StubStoreInfo(stores.ToArray());

        var result = await Build().GetMyStoresAsync(_deviceId);
        Assert.Equal(50, result.Count);
    }

    // ═══ 11. Tenant đã xóa/không resolve được → bỏ khỏi list ═══════════════
    [Fact]
    public async Task Missing_Tenant_Info_Skipped()
    {
        var ghost = Guid.NewGuid();
        SeedOrder(ghost); // order tồn tại nhưng tenant không resolve được
        StubStoreInfo(); // map rỗng — GetStoreInfoMapAsync không trả ghost

        var result = await Build().GetMyStoresAsync(_deviceId);
        Assert.Empty(result);
    }

    // ═══ 12. Không có alliance wallet → Silo vẫn hoạt động ═════════════════
    [Fact]
    public async Task No_Alliance_Wallet_Silo_Still_Works()
    {
        var t1 = SeedTenant("Silo bình thường", "silo");
        SeedCustomerWithPoints(t1, 42);
        StubStoreInfo((t1, "Silo bình thường", "silo"));
        // wallet mock trả null mặc định — không setup

        var store = Assert.Single(await Build().GetMyStoresAsync(_deviceId));
        Assert.Equal(42, store.PointBalance);
        Assert.Equal("Silo", store.Mode);
    }
}
