using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.CoreHub.Services.Onboarding.Strategies;
using Xunit;

namespace VanAn.Core.Tests.Services.Onboarding;

/// <summary>
/// Issue #179: OnboardingService template seeding — ApplyTemplateAsync (F&B template),
/// idempotency, unknown template, and GetSeedCountsAsync (real DB counts for the
/// QuickSetup success screen — no more hardcoded template metadata).
/// </summary>
public class OnboardingTemplateServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly OnboardingService _service;
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid FnbTemplateId = Guid.Parse("a1111111-1111-1111-1111-111111111111");

    public OnboardingTemplateServiceTests()
    {
        _connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        _connection.Open();

        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();

        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseInternalServiceProvider(efServiceProvider).UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new VanAnDbContext(options);
        _context.Database.EnsureCreated();

        IEnumerable<IIndustrySeedStrategy> strategies = new IIndustrySeedStrategy[] { new FnbSeedStrategy() };
        _service = new OnboardingService(_context, strategies, NullLogger<OnboardingService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact(DisplayName = "T1 (#179): ApplyTemplate seeds F&B template and GetSeedCounts reads real counts")]
    public async Task ApplyTemplate_SeedsFnb_GetSeedCountsMatches()
    {
        var result = await _service.ApplyTemplateAsync(FnbTemplateId, TenantId);

        Assert.NotNull(result);

        // Real DB counts — exactly what QuickSetup now shows on the success screen.
        var counts = await _service.GetSeedCountsAsync(TenantId);
        Assert.Equal(32, counts.Products);
        Assert.Equal(15, counts.Ingredients);
        Assert.True(counts.Recipes > 0);
    }

    [Fact(DisplayName = "T2 (#179): ApplyTemplate is idempotent — second call skips seeding")]
    public async Task ApplyTemplate_SecondCall_Skips()
    {
        await _service.ApplyTemplateAsync(FnbTemplateId, TenantId);
        var result = await _service.ApplyTemplateAsync(FnbTemplateId, TenantId);

        Assert.Contains("already seeded", result.Name, StringComparison.OrdinalIgnoreCase);

        var counts = await _service.GetSeedCountsAsync(TenantId);
        Assert.Equal(32, counts.Products);
    }

    [Fact(DisplayName = "T3 (#179): ApplyTemplate unknown template throws ArgumentException")]
    public async Task ApplyTemplate_UnknownTemplate_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ApplyTemplateAsync(Guid.NewGuid(), TenantId));
    }

    [Fact(DisplayName = "T4 (#179): GetSeedCounts for empty tenant returns zeros")]
    public async Task GetSeedCounts_EmptyTenant_ReturnsZeros()
    {
        var counts = await _service.GetSeedCountsAsync(Guid.NewGuid());
        Assert.Equal(0, counts.Products);
        Assert.Equal(0, counts.Ingredients);
        Assert.Equal(0, counts.Recipes);
    }
}
