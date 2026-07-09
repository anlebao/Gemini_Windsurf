using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VanAn.ShopERP.Infrastructure
{
    /// <summary>
    /// Design-time DbContext factory for EF Core CLI tools (dotnet ef migrations add/update).
    /// Allows `dotnet ef` to instantiate ShopERPDbContext without runtime DI (DataProtection, etc.).
    /// Reads connection string from SQLITE_DB_PATH or ConnectionStrings__DefaultConnection env var,
    /// falls back to local SQLite file.
    /// Provider is always SQLite (ShopERP is SQLite-only edge node per architecture).
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ShopERPDbContext>
    {
        public ShopERPDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ShopERPDbContext>();

            // Read connection string from environment (set by Docker/dev scripts) or use local default.
            // SQLITE_DB_PATH is the Docker compose env var; ConnectionStrings__DefaultConnection is the .NET convention.
            string connectionString =
                Environment.GetEnvironmentVariable("SQLITE_DB_PATH")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? "Data Source=vanan_shoperp.db";

            _ = optionsBuilder.UseSqlite(connectionString);

            return new ShopERPDbContext(optionsBuilder.Options);
        }
    }
}
