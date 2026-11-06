using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VanAn.TenantAi.Infra;

/// <summary>
/// Design-time factory for `dotnet ef` — reads ConnectionStrings__TenantAi with a
/// localhost fallback (scaffolding never connects; runtime apps configure via DI).
/// </summary>
public sealed class TenantAiDbContextFactory : IDesignTimeDbContextFactory<TenantAiDbContext>
{
    public TenantAiDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__TenantAi")
            ?? "Host=localhost;Port=5432;Database=vanan_ai;Username=vanan_ai;Password=postgres";
        var options = new DbContextOptionsBuilder<TenantAiDbContext>()
            .UseNpgsql(conn, npg => npg.UseVector())
            .UseSnakeCaseNamingConvention()
            .Options;
        return new TenantAiDbContext(options);
    }
}
