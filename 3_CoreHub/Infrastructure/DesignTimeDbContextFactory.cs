using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VanAn.CoreHub.Infrastructure
{
    /// <summary>
    /// Design-time DbContext factory for EF Core CLI tools (dotnet ef migrations add/update).
    /// Allows `dotnet ef` to instantiate VanAnDbContext without runtime dependencies (ITenantProvider, etc.).
    /// Reads connection string from ConnectionStrings__DefaultConnection env var, falls back to SQLite default.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VanAnDbContext>
    {
        public VanAnDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<VanAnDbContext>();

            // Read connection string from environment (set by dev ops) or use default
            string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? "Data Source=vanan_shoperp.db";

            optionsBuilder.UseSqlite(connectionString);

            return new VanAnDbContext(optionsBuilder.Options);
        }
    }
}
