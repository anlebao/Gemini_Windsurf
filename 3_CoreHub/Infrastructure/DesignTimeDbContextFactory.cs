using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VanAn.CoreHub.Infrastructure
{
    /// <summary>
    /// Design-time DbContext factory for EF Core CLI tools (dotnet ef migrations add/update).
    /// Allows `dotnet ef` to instantiate VanAnDbContext without runtime dependencies (ITenantProvider, etc.).
    /// Reads connection string from ConnectionStrings__DefaultConnection env var, falls back to PostgreSQL default.
    /// Provider auto-detected from connection string prefix (matches Gateway Program.cs runtime behavior):
    ///   "Data Source=" → SQLite (local dev / tests)
    ///   "Host="        → Npgsql (production / staging)
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VanAnDbContext>
    {
        public VanAnDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<VanAnDbContext>();

            // Read connection string from environment (set by dev ops) or use PostgreSQL default.
            // Default to PostgreSQL because it is the production target (Option B monolithic mode).
            string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=VanAnLocal;Username=vanan_dev;Password=VanAnLocal@2026";

            // Auto-detect provider: SQLite ("Data Source=") for local dev, Npgsql ("Host=") for production.
            // This matches the runtime auto-detect logic in 2_Gateway/Program.cs.
            if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                optionsBuilder.UseSqlite(connectionString);
            else
                optionsBuilder.UseNpgsql(connectionString);

            return new VanAnDbContext(optionsBuilder.Options);
        }
    }
}
