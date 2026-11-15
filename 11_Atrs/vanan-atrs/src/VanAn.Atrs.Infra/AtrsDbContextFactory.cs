using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VanAn.Atrs.Infra;

/// <summary>
/// Design-time factory cho `dotnet ef migrations` — service chưa có DI host
/// migrations thời điểm build. Connection mặc định cho dev; prod dùng env var.
/// </summary>
public sealed class AtrsDbContextFactory : IDesignTimeDbContextFactory<AtrsDbContext>
{
    public AtrsDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Atrs")
            ?? "Host=localhost;Port=5432;Database=vanan_atrs;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseNpgsql(connection)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AtrsDbContext(options);
    }
}