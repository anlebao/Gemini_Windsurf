using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// Instance Feature Gate (2026-11-27): SysAdmin NavFlags override tenant ShopFeatureSettings
/// for shared concepts — kill-switch semantics (instance OFF → tenant cannot show).
/// Resolution: X-KhachLink-Domain request header → KhachLinkInstances.NavFlags (cached).
/// </summary>
public class InstanceFeatureGateTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _sp;
    private readonly VanAnDbContext _db;

    public InstanceFeatureGateTests()
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

    private InstanceFeatureGate BuildGate(string? domainHeader)
    {
        var httpContext = new DefaultHttpContext();
        if (domainHeader != null)
            httpContext.Request.Headers[InstanceFeatureGate.DomainHeader] = domainHeader;

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new InstanceFeatureGate(accessor, _db, new MemoryCache(new MemoryCacheOptions()),
            NullLogger<InstanceFeatureGate>.Instance);
    }

    private KhachLinkInstance SeedInstance(string domain, KhachLinkNavFlags flags, bool isActive = true)
    {
        var instance = new KhachLinkInstance("Test Portal", KhachLinkProfile.FullCommerce, domain,
            navFlagsOverride: flags);
        if (!isActive)
            instance.Deactivate();
        _db.KhachLinkInstances.Add(instance);
        _db.SaveChanges();
        return instance;
    }

    private static ShopFeatureSettingsDto AllOn() => new()
    {
        Campaign_Section_Enabled = true,
        Home_CampaignSection_Enabled = true,
        Home_StoreSection_Enabled = true,
        QR_TableNumber_Enabled = true,
        VibeShowcase_Section_Enabled = true,
        Loyalty_Program_Enabled = true
    };

    [Fact(DisplayName = "Gate: no X-KhachLink-Domain header → null flags → settings unchanged")]
    public async Task NoHeader_ReturnsNull_SettingsUnchanged()
    {
        var gate = BuildGate(domainHeader: null);
        var flags = await gate.GetNavFlagsAsync();

        Assert.Null(flags);

        var settings = gate.ApplyOverrides(AllOn(), flags);
        Assert.True(settings.Campaign_Section_Enabled);
        Assert.True(settings.QR_TableNumber_Enabled);
    }

    [Fact(DisplayName = "Gate: unknown domain → null flags → settings unchanged")]
    public async Task UnknownDomain_ReturnsNull()
    {
        var gate = BuildGate("unknown.example.com");
        var flags = await gate.GetNavFlagsAsync();

        Assert.Null(flags);
        Assert.True(gate.ApplyOverrides(AllOn(), flags).Campaign_Section_Enabled);
    }

    [Fact(DisplayName = "Gate: instance ShowCampaigns=false → Campaign + Home_Campaign forced off (kill-switch)")]
    public async Task CampaignsOff_ForcesBothCampaignFlagsOff()
    {
        SeedInstance("shopai.test", new KhachLinkNavFlags { ShowCampaigns = false });
        var gate = BuildGate("shopai.test");

        var flags = await gate.GetNavFlagsAsync();
        Assert.NotNull(flags);

        var settings = gate.ApplyOverrides(AllOn(), flags);
        Assert.False(settings.Campaign_Section_Enabled);
        Assert.False(settings.Home_CampaignSection_Enabled);
        // Unmapped flags untouched
        Assert.True(settings.VibeShowcase_Section_Enabled);
        Assert.True(settings.Loyalty_Program_Enabled);
    }

    [Fact(DisplayName = "Gate: instance ShowStores=false → Home_StoreSection off; ShowScan=false → QR_TableNumber off")]
    public async Task StoresAndScanOff_ForcesMappedFlagsOff()
    {
        SeedInstance("portal.test", new KhachLinkNavFlags { ShowStores = false, ShowScan = false });
        var gate = BuildGate("portal.test");

        var settings = gate.ApplyOverrides(AllOn(), await gate.GetNavFlagsAsync());
        Assert.False(settings.Home_StoreSection_Enabled);
        Assert.False(settings.QR_TableNumber_Enabled);
        Assert.True(settings.Campaign_Section_Enabled);
    }

    [Fact(DisplayName = "Gate: instance ON + tenant OFF → stays OFF (tenant autonomy preserved)")]
    public async Task InstanceOn_TenantOff_StaysOff()
    {
        SeedInstance("portal.test", new KhachLinkNavFlags()); // all ON (FullCommerce)
        var gate = BuildGate("portal.test");

        var tenantSettings = AllOn();
        tenantSettings.Campaign_Section_Enabled = false;
        tenantSettings.QR_TableNumber_Enabled = false;

        var settings = gate.ApplyOverrides(tenantSettings, await gate.GetNavFlagsAsync());
        Assert.False(settings.Campaign_Section_Enabled);
        Assert.False(settings.QR_TableNumber_Enabled);
    }

    [Fact(DisplayName = "Gate: inactive instance → null flags → no override (consistent with by-domain 404)")]
    public async Task InactiveInstance_ReturnsNull()
    {
        SeedInstance("dead.test", new KhachLinkNavFlags { ShowCampaigns = false }, isActive: false);
        var gate = BuildGate("dead.test");

        var flags = await gate.GetNavFlagsAsync();
        Assert.Null(flags);
        Assert.True(gate.ApplyOverrides(AllOn(), flags).Campaign_Section_Enabled);
    }

    [Fact(DisplayName = "Gate: domain header case-insensitive + whitespace tolerant")]
    public async Task DomainHeader_CaseInsensitive()
    {
        SeedInstance("shopai.test", new KhachLinkNavFlags { ShowCampaigns = false });
        var gate = BuildGate("  SHOPAI.Test ");

        var flags = await gate.GetNavFlagsAsync();
        Assert.NotNull(flags);
        Assert.False(flags.ShowCampaigns);
    }

    [Fact(DisplayName = "HomeGate: instance ShowStores=false → home store section off (kill-switch)")]
    public async Task HomeOverrides_StoresOff_ForcesStoreSectionOff()
    {
        SeedInstance("portal.test", new KhachLinkNavFlags { ShowStores = false });
        var gate = BuildGate("portal.test");

        var (campaign, store) = gate.ApplyHomeOverrides(
            campaignEnabled: true, storeEnabled: true, await gate.GetNavFlagsAsync());

        Assert.False(store);
        Assert.True(campaign); // unmapped flag untouched
    }

    [Fact(DisplayName = "HomeGate: instance ShowCampaigns=false → home campaign section off")]
    public async Task HomeOverrides_CampaignsOff_ForcesCampaignSectionOff()
    {
        SeedInstance("portal.test", new KhachLinkNavFlags { ShowCampaigns = false });
        var gate = BuildGate("portal.test");

        var (campaign, store) = gate.ApplyHomeOverrides(
            campaignEnabled: true, storeEnabled: true, await gate.GetNavFlagsAsync());

        Assert.False(campaign);
        Assert.True(store);
    }

    [Fact(DisplayName = "HomeGate: null flags → unchanged; instance ON does not force ON")]
    public async Task HomeOverrides_NullFlagsAndGlobalOff_StaysConsistent()
    {
        var gate = BuildGate(domainHeader: null);

        // null flags → returned as-is (including OFF values)
        Assert.Equal((true, false), gate.ApplyHomeOverrides(true, false, null));

        // instance all-ON + global OFF → stays OFF (no force-on)
        SeedInstance("portal.test", new KhachLinkNavFlags());
        var flags = await gate.GetNavFlagsAsync();
        Assert.Equal((true, false), gate.ApplyHomeOverrides(true, false, flags));
    }
}
