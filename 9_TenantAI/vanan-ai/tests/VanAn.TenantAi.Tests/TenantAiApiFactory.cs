using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VanAn.TenantAi.Domain;
using VanAn.TenantAi.Infra;

namespace VanAn.TenantAi.Tests;

/// <summary>In-memory host for auth tests. CallerKeys seeded trực tiếp qua DI scope
/// (bootstrap env path không đáng tin trong WAF — config timing).</summary>
public sealed class TenantAiApiFactory : WebApplicationFactory<Program>
{
    public const string GatewayKey = "gw-test-key";
    public const string ShopKey = "shop-test-key";
    public const string SigningSecret = "test-signing-secret";
    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private string _dbName = "tenantai-test-" + Guid.NewGuid();
    private bool _seeded;
    private Action<IServiceCollection>? _configure;
    private Dictionary<string, string?>? _settings;

    /// <summary>xUnit fixtures allow one public ctor — service overrides via factory helper instead.</summary>
    public static TenantAiApiFactory WithServices(Action<IServiceCollection> configure)
        => new() { _configure = configure };

    /// <summary>Same + app settings overrides (e.g. TenantAi:Enabled=false for kill-switch tests).</summary>
    public static TenantAiApiFactory With(
        Action<IServiceCollection>? configure = null, Dictionary<string, string?>? settings = null)
        => new() { _configure = configure, _settings = settings };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("TenantAi:CtxSigningSecret", SigningSecret);
        if (_settings != null)
            foreach (var (k, v) in _settings)
                builder.UseSetting(k, v);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TenantAiDbContext>>();
            services.RemoveAll<TenantAiDbContext>();
            services.AddDbContext<TenantAiDbContext>(o => o.UseInMemoryDatabase(_dbName));
            _configure?.Invoke(services);
        });
    }

    private void EnsureSeeded()
    {
        if (_seeded) return;
        _ = Server; // force host build
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantAiDbContext>();
        db.CallerKeys.Add(CallerKey.Create("gateway", Sha256Hex(GatewayKey),
            scopesJson: "[\"public-chat\"]", tenantScopeJson: null));
        db.CallerKeys.Add(CallerKey.Create("shoperp-a", Sha256Hex(ShopKey),
            scopesJson: "[\"ingest\",\"admin\"]",
            tenantScopeJson: $"[\"{TenantA}\"]"));
        db.SaveChanges();
        _seeded = true;
    }

    public new HttpClient CreateClient()
    {
        var client = base.CreateClient();
        EnsureSeeded();
        return client;
    }

    private static string Sha256Hex(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
