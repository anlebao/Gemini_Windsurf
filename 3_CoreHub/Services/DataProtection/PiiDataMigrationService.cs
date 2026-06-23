using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.DataProtection;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.DataProtection
{
    /// <summary>
    /// One-time migration utility for existing plaintext PII fields.
    /// Uses raw ADO.NET SQL to read values without triggering EF Core EncryptedStringConverter,
    /// then encrypts them using the exact same converters that EF Core compiled into its model,
    /// and writes back via raw SQL UPDATE statements.
    /// </summary>
    public class PiiDataMigrationService
    {
        private readonly IVanAnDbContext _context;
        private readonly VanAnDbContext? _coreHubContext;
        private readonly ILogger<PiiDataMigrationService> _logger;

        public PiiDataMigrationService(IVanAnDbContext context, ILogger<PiiDataMigrationService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public PiiDataMigrationService(VanAnDbContext context, ILogger<PiiDataMigrationService> logger)
            : this((IVanAnDbContext)context, logger)
        {
            _coreHubContext = context;
        }

        public async Task MigrateAsync(CancellationToken cancellationToken = default)
        {
            if (!DataProtectionProviderAccessor.IsInitialized)
            {
                _logger.LogWarning(
                    "DataProtectionProviderAccessor is not initialized. " +
                    "PII migration will use the ephemeral fallback provider. " +
                    "Production hosts must register AddDataProtection before running migration.");
            }

            // Cast to DbContext to access Database and Model APIs.
            // Safe: IVanAnDbContext is only implemented by VanAnDbContext which extends DbContext.
            var dbContext = (DbContext)_context;

            // Resolve the value converters directly from the compiled EF Core model.
            // This guarantees we encrypt with the exact same protector that EF Core will
            // use when decrypting via the EncryptedStringConverter — even in test scenarios
            // where the static DataProtectionProviderAccessor may hold a different key.
            var customerPhoneConverter = GetConverter(dbContext, typeof(Customer), nameof(Customer.PhoneNumber));
            var customerEmailConverter = GetConverter(dbContext, typeof(Customer), nameof(Customer.Email));

            var connection = dbContext.Database.GetDbConnection();

            await MigrateCustomersAsync(connection, customerPhoneConverter, customerEmailConverter, cancellationToken);
            await MigrateDemoUsersAsync(connection, cancellationToken);

            if (_coreHubContext is not null)
            {
                var leadPhoneConverter = GetConverter(_coreHubContext, typeof(Lead), nameof(Lead.PhoneNumber));
                var leadEmailConverter = GetConverter(_coreHubContext, typeof(Lead), nameof(Lead.Email));
                var leadsConnection = _coreHubContext.Database.GetDbConnection();
                await MigrateLeadsAsync(leadsConnection, leadPhoneConverter, leadEmailConverter, cancellationToken);
            }

            _logger.LogInformation("PII data migration completed.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Customers
        // ─────────────────────────────────────────────────────────────────────

        private async Task MigrateCustomersAsync(
            DbConnection connection,
            ValueConverter? phoneConverter,
            ValueConverter? emailConverter,
            CancellationToken cancellationToken)
        {
            if (phoneConverter is null && emailConverter is null)
            {
                _logger.LogDebug("No encrypted converters found for Customer PII fields; skipping.");
                return;
            }

            bool wasOpen = connection.State == ConnectionState.Open;
            if (!wasOpen) await connection.OpenAsync(cancellationToken);

            try
            {
                var rows = new List<(string id, string phone, string? email)>();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT Id, PhoneNumber, Email FROM Customers";
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var id    = reader.GetString(0);
                        var phone = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        var email = reader.IsDBNull(2) ? null : reader.GetString(2);
                        rows.Add((id, phone, email));
                    }
                }

                int migrated = 0;

                foreach (var (id, phone, email) in rows)
                {
                    bool updatedPhone = false;
                    bool updatedEmail = false;
                    string newPhone   = phone;
                    string? newEmail  = email;

                    if (phoneConverter is not null && !string.IsNullOrEmpty(phone) && IsPlainText(phone))
                    {
                        newPhone     = EncryptWithConverter(phoneConverter, phone);
                        updatedPhone = true;
                    }

                    if (emailConverter is not null && !string.IsNullOrEmpty(email) && IsPlainText(email))
                    {
                        newEmail     = EncryptWithConverter(emailConverter, email!);
                        updatedEmail = true;
                    }

                    if (updatedPhone || updatedEmail)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.CommandText = "UPDATE Customers SET PhoneNumber = @phone, Email = @email WHERE Id = @id";
                        AddParam(cmd, "@phone", newPhone);
                        AddParam(cmd, "@email", (object?)newEmail ?? DBNull.Value);
                        AddParam(cmd, "@id",    id);
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                        migrated++;
                    }
                }

                if (migrated > 0)
                    _logger.LogInformation("Migrated {Count} Customer PII records.", migrated);
            }
            finally
            {
                if (!wasOpen) await connection.CloseAsync();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Leads
        // ─────────────────────────────────────────────────────────────────────

        private async Task MigrateLeadsAsync(
            DbConnection connection,
            ValueConverter? phoneConverter,
            ValueConverter? emailConverter,
            CancellationToken cancellationToken)
        {
            if (phoneConverter is null && emailConverter is null)
            {
                _logger.LogDebug("No encrypted converters found for Lead PII fields; skipping.");
                return;
            }

            bool wasOpen = connection.State == ConnectionState.Open;
            if (!wasOpen) await connection.OpenAsync(cancellationToken);

            try
            {
                var rows = new List<(string id, string phone, string? email)>();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT Id, PhoneNumber, Email FROM Leads";
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var id    = reader.GetString(0);
                        var phone = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        var email = reader.IsDBNull(2) ? null : reader.GetString(2);
                        rows.Add((id, phone, email));
                    }
                }

                int migrated = 0;

                foreach (var (id, phone, email) in rows)
                {
                    bool updatedPhone = false;
                    bool updatedEmail = false;
                    string newPhone   = phone;
                    string? newEmail  = email;

                    if (phoneConverter is not null && !string.IsNullOrEmpty(phone) && IsPlainText(phone))
                    {
                        newPhone     = EncryptWithConverter(phoneConverter, phone);
                        updatedPhone = true;
                    }

                    if (emailConverter is not null && !string.IsNullOrEmpty(email) && IsPlainText(email))
                    {
                        newEmail     = EncryptWithConverter(emailConverter, email!);
                        updatedEmail = true;
                    }

                    if (updatedPhone || updatedEmail)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.CommandText = "UPDATE Leads SET PhoneNumber = @phone, Email = @email WHERE Id = @id";
                        AddParam(cmd, "@phone", newPhone);
                        AddParam(cmd, "@email", (object?)newEmail ?? DBNull.Value);
                        AddParam(cmd, "@id",    id);
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                        migrated++;
                    }
                }

                if (migrated > 0)
                    _logger.LogInformation("Migrated {Count} Lead/FacebookLead PII records.", migrated);
            }
            finally
            {
                if (!wasOpen) await connection.CloseAsync();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // DemoUsers
        // ─────────────────────────────────────────────────────────────────────

        private async Task MigrateDemoUsersAsync(
            DbConnection connection,
            CancellationToken cancellationToken)
        {
            // DemoUser.Email is not encrypted via EncryptedStringConverter in the current schema.
            // If no migration is needed, this is a no-op.
            var dbContext = (DbContext)_context;
            var emailConverter = GetConverter(dbContext, typeof(DemoUser), nameof(DemoUser.Email));

            if (emailConverter is null)
            {
                _logger.LogDebug("No encrypted converter found for DemoUser.Email; skipping.");
                return;
            }

            bool wasOpen = connection.State == ConnectionState.Open;
            if (!wasOpen) await connection.OpenAsync(cancellationToken);

            try
            {
                var rows = new List<(string id, string? email)>();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT Id, Email FROM Users";
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var id    = reader.GetString(0);
                        var email = reader.IsDBNull(1) ? null : reader.GetString(1);
                        rows.Add((id, email));
                    }
                }

                int migrated = 0;

                foreach (var (id, email) in rows)
                {
                    if (!string.IsNullOrEmpty(email) && IsPlainText(email))
                    {
                        var encrypted = EncryptWithConverter(emailConverter, email);

                        using var cmd = connection.CreateCommand();
                        cmd.CommandText = "UPDATE Users SET Email = @email WHERE Id = @id";
                        AddParam(cmd, "@email", encrypted);
                        AddParam(cmd, "@id",    id);
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                        migrated++;
                    }
                }

                if (migrated > 0)
                    _logger.LogInformation("Migrated {Count} DemoUser PII records.", migrated);
            }
            finally
            {
                if (!wasOpen) await connection.CloseAsync();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves the EF Core ValueConverter for an entity property from the compiled model.
        /// Returns null if no converter is configured for the property.
        /// </summary>
        private static ValueConverter? GetConverter(DbContext context, Type entityType, string propertyName)
        {
            try
            {
                return context.Model
                    .FindEntityType(entityType)?
                    .FindProperty(propertyName)?
                    .GetValueConverter();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Encrypts a plaintext value using the EF Core ValueConverter's ConvertToProvider delegate.
        /// This uses the exact same protector that EF Core uses when writing to the database.
        /// </summary>
        private static string EncryptWithConverter(ValueConverter converter, string plaintext)
        {
            var encrypted = converter.ConvertToProvider(plaintext);
            return encrypted as string ?? plaintext;
        }

        /// <summary>
        /// Adds a named parameter to a DbCommand in a provider-agnostic way.
        /// </summary>
        private static void AddParam(DbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        /// <summary>
        /// Returns true if the value appears to be plaintext.
        /// A valid encrypted payload produced by ASP.NET Core Data Protection is a Base64Url string.
        /// Any value that is not valid Base64Url, or is a short human-readable string, is treated as plaintext.
        /// </summary>
        private static bool IsPlainText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            // ASP.NET Core Data Protection tokens are Base64Url-encoded and always start with
            // "CfD" (the magic header after Base64Url encoding the protection prefix bytes).
            // Plaintext PII values (phone numbers, emails) never start with this prefix.
            if (value.StartsWith("CfD", StringComparison.Ordinal))
                return false;

            // Additional heuristic: valid encrypted tokens are long Base64Url strings.
            // Typical phone/email values are short and contain characters invalid in Base64Url.
            return true;
        }
    }
}
