using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using VanAn.Voice.Api.Services;
using VanAn.Voice.Domain;
using VanAn.Voice.Infra;

namespace VanAn.Voice.Tests;

/// <summary>Mints HS256 staff JWTs with the same shape as JwtTokenService (sub + tenant_id).</summary>
public static class TestJwt
{
    public const string Secret = "voice-test-secret-key-0123456789-abcdef-123456";
    public const string Issuer = "VanAnShopERP";
    public const string Audience = "VanAnApi";

    public static string Mint(Guid userId, Guid tenantId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            Issuer, Audience,
            new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim("tenant_id", tenantId.ToString())
            },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>Fake context verifier — tests control whether the context verifies and which tenant it yields.</summary>
public sealed class FakeContextVerifier : IContextVerifier
{
    public Guid? VerifiedTenantId { get; set; }
    public bool Fail { get; set; }
    public Guid? ResolvedCounterpart { get; set; }

    public Task<ContextVerification?> VerifyAsync(string contextType, string contextReferenceId, Guid callerUserId, Guid targetUserId, CancellationToken ct = default)
        => Task.FromResult<ContextVerification?>(
            Fail ? new ContextVerification(false, null, null)
                 : new ContextVerification(VerifiedTenantId != null, VerifiedTenantId, "verified"));

    public Task<Guid?> ResolveCounterpartAsync(string contextType, string contextReferenceId, Guid callerUserId, CancellationToken ct = default)
        => Task.FromResult(ResolvedCounterpart);
}

/// <summary>Thread-safe recording publisher — captures (connectionId, method, arg) for assertions.</summary>
public sealed class RecordingPublisher : ICallEventPublisher
{
    private readonly object _lock = new();
    public List<(string? ConnectionId, string Method, object? Arg)> Sent { get; } = new();

    public Task SendToAsync(string? connectionId, string method, object? arg)
    {
        lock (_lock) Sent.Add((connectionId, method, arg));
        return Task.CompletedTask;
    }

    public Task SendToConnectionsAsync(IEnumerable<string> connectionIds, string method, object? arg)
    {
        lock (_lock)
        {
            foreach (var id in connectionIds) Sent.Add((id, method, arg));
        }
        return Task.CompletedTask;
    }

    public IReadOnlyList<(string? ConnectionId, string Method, object? Arg)> SentTo(string? connectionId)
    {
        lock (_lock) return Sent.Where(s => s.ConnectionId == connectionId).ToList();
    }
}

/// <summary>
/// Test harness: real SQLite file + real schema DDL + real state store; fake publisher and
/// context verifier; metering disabled by default (exercises the fail-open path).
/// </summary>
public sealed class CallServiceHarness : IDisposable
{
    public string DbPath { get; }
    public VoiceStateStore Store { get; }
    public TenantFeatureStore Features { get; }
    public RecordingPublisher Publisher { get; }
    public FakeContextVerifier ContextVerifier { get; } = new();
    public IConfiguration Configuration { get; }
    public ServiceProvider Services { get; }
    public IServiceScope Scope { get; }
    public VoiceDbContext Db { get; }
    public CallService Service { get; }

    public CallServiceHarness(Action<IServiceCollection>? configure = null, IConfiguration? configuration = null)
    {
        DbPath = Path.Combine(Path.GetTempPath(), $"voice-test-{Guid.NewGuid():N}.db");
        Configuration = configuration ?? new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Voice:DbPath"] = DbPath,
            ["Voice:RingTimeoutSeconds"] = "30",
            ["Voice:NegotiationTimeoutSeconds"] = "20",
            ["Voice:MaxDurationMinutes"] = "120",
            ["Voice:ReconnectWindowSeconds"] = "10",
            ["Voice:MaxIceRestarts"] = "2",
            ["Voice:EstimatedMinutesPerCall"] = "3"
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(Configuration);
        services.AddDbContext<VoiceDbContext>(o => o.UseSqlite($"Data Source={DbPath}"));
        services.AddSingleton<VoiceStateStore>();
        services.AddSingleton<TenantFeatureStore>();
        services.AddSingleton<RateLimiter>();
        services.AddSingleton<ICallEventPublisher, RecordingPublisher>();
        services.AddScoped<IContextVerifier>(_ => ContextVerifier);
        // Metering not configured → disabled client → fail-open (CheckAsync null = allow).
        services.AddHttpClient<MeteringClient>(client => client.Timeout = TimeSpan.FromSeconds(3));
        services.AddScoped<PermissionService>();
        services.AddScoped<CallService>();
        configure?.Invoke(services);
        Services = services.BuildServiceProvider();

        Scope = Services.CreateScope();
        Db = Scope.ServiceProvider.GetRequiredService<VoiceDbContext>();
        Db.EnsureVoiceSchema();

        Store = Services.GetRequiredService<VoiceStateStore>();
        Features = Services.GetRequiredService<TenantFeatureStore>();
        // Most call-mechanics tests call without a business context — enable open calling.
        Features.Set(VoiceIdentities.Tenant, new VoiceTenantConfig(VoiceOpenCalling: true));
        Publisher = (RecordingPublisher)Services.GetRequiredService<ICallEventPublisher>();
        Service = Scope.ServiceProvider.GetRequiredService<CallService>();
    }

    public void Dispose()
    {
        Scope.Dispose();
        Services.Dispose();
        try { File.Delete(DbPath); } catch { /* best effort */ }
    }
}

public static class VoiceIdentities
{
    public static readonly Guid Tenant = Guid.NewGuid();
    public static readonly Guid UserA = Guid.NewGuid();
    public static readonly Guid UserB = Guid.NewGuid();
    public static readonly Guid UserC = Guid.NewGuid();

    public static VoiceIdentity Staff(Guid userId) => new(userId, VoiceIdentityKind.Staff, Tenant);
    public static VoiceIdentity Customer(Guid userId) => new(userId, VoiceIdentityKind.Customer);
}