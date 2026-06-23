using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using VanAn.CoreHub.Infrastructure.DataProtection;
using VanAn.CoreHub.Services.DataProtection;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using TenantAggregate = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using Microsoft.Extensions.Logging;
using Xunit;

namespace VanAn.Integration.Tests.Services
{
    /// <summary>
    /// Wave 2: PII field-level encryption integration tests.
    /// Verifies that Customer/Lead/DemoUser PII is encrypted at rest and decrypted on read.
    /// </summary>
    public class CustomerEncryptionTests : IntegrationTestBase
    {
        [Fact(DisplayName = "W2-T7: Customer PhoneNumber is encrypted at rest and decrypted on read")]
        public async Task Customer_PhoneNumber_IsEncryptedAtRest_AndDecryptedOnRead()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var tenant = TenantAggregate.CreateCompany(new TenantId(tenantId), "Test Tenant");
            _dbContext.Tenants.Add(tenant);
            await _dbContext.SaveChangesAsync();

            var customer = TestEntityBuilder.CreateCustomer(new TenantId(tenantId));
            const string plainPhone = "+84-912-345-678";
            customer.GetType().GetProperty(nameof(Customer.PhoneNumber))!
                .SetValue(customer, plainPhone);
            _dbContext.Customers.Add(customer);
            await _dbContext.SaveChangesAsync();

            // Act: Read raw value from database (keep connection open — closing destroys in-memory DB)
            var connection = _dbContext.Database.GetDbConnection();
            bool wasOpen = connection.State == ConnectionState.Open;
            if (!wasOpen) await connection.OpenAsync();
            string? rawValue;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT PhoneNumber FROM Customers WHERE Id = @id";
                AddParameter(command, "@id", customer.Id);
                rawValue = (string?)await command.ExecuteScalarAsync();
            }
            if (!wasOpen) await connection.CloseAsync();

            // Assert
            Assert.NotNull(rawValue);
            Assert.NotEqual(plainPhone, rawValue);
            Assert.True(rawValue.Length > plainPhone.Length, "Encrypted value should be longer than plaintext");

            // Act: Read via EF Core (should decrypt)
            var savedCustomer = await _dbContext.Customers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == customer.Id);

            Assert.NotNull(savedCustomer);
            Assert.Equal(plainPhone, savedCustomer.PhoneNumber);
        }

        [Fact(DisplayName = "W2-T7: Customer Email is encrypted at rest and decrypted on read")]
        public async Task Customer_Email_IsEncryptedAtRest_AndDecryptedOnRead()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var tenant = TenantAggregate.CreateCompany(new TenantId(tenantId), "Test Tenant");
            _dbContext.Tenants.Add(tenant);
            await _dbContext.SaveChangesAsync();

            var customer = TestEntityBuilder.CreateCustomer(new TenantId(tenantId));
            const string plainEmail = "customer@example.com";
            customer.GetType().GetProperty(nameof(Customer.Email))!
                .SetValue(customer, plainEmail);
            _dbContext.Customers.Add(customer);
            await _dbContext.SaveChangesAsync();

            // Act: Read raw value from database (keep connection open — closing destroys in-memory DB)
            var connection = _dbContext.Database.GetDbConnection();
            bool wasOpen = connection.State == ConnectionState.Open;
            if (!wasOpen) await connection.OpenAsync();
            string? rawValue;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Email FROM Customers WHERE Id = @id";
                AddParameter(command, "@id", customer.Id);
                rawValue = (string?)await command.ExecuteScalarAsync();
            }
            if (!wasOpen) await connection.CloseAsync();

            // Assert
            Assert.NotNull(rawValue);
            Assert.NotEqual(plainEmail, rawValue);

            // Act: Read via EF Core (should decrypt)
            var savedCustomer = await _dbContext.Customers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == customer.Id);

            Assert.NotNull(savedCustomer);
            Assert.Equal(plainEmail, savedCustomer.Email);
        }

        [Fact(DisplayName = "W2-T7: Lead PhoneNumber and Email are encrypted at rest")]
        public async Task Lead_Pii_IsEncryptedAtRest()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var lead = TestEntityBuilder.CreateLead(tenantId, "+84-912-345-679", "lead@example.com");
            _dbContext.Leads.Add(lead);
            await _dbContext.SaveChangesAsync();

            // Act: Read raw values from database (keep connection open — closing destroys in-memory DB)
            var connection = _dbContext.Database.GetDbConnection();
            bool wasOpen = connection.State == ConnectionState.Open;
            if (!wasOpen) await connection.OpenAsync();
            string rawPhone;
            string? rawEmail;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT PhoneNumber, Email FROM Leads WHERE Id = @id";
                AddParameter(command, "@id", lead.Id);
                using var reader = await command.ExecuteReaderAsync();
                await reader.ReadAsync();
                rawPhone = reader.GetString(0);
                rawEmail = reader.IsDBNull(1) ? null : reader.GetString(1);
            }
            if (!wasOpen) await connection.CloseAsync();

            // Assert
            Assert.NotEqual(lead.PhoneNumber, rawPhone);
            Assert.NotEqual(lead.Email, rawEmail);

            // Act: Read via EF Core (should decrypt)
            var savedLead = await _dbContext.Leads
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(l => l.Id == lead.Id);

            Assert.NotNull(savedLead);
            Assert.Equal(lead.PhoneNumber, savedLead.PhoneNumber);
            Assert.Equal(lead.Email, savedLead.Email);
        }

        [Fact(DisplayName = "W2-T7: PiiDataMigrationService encrypts existing plaintext records")]
        public async Task PiiDataMigrationService_EncryptsExistingPlaintextRecords()
        {
            // Arrange: Insert a customer with raw plaintext values bypassing EF Core converters
            var tenantId = Guid.NewGuid();
            var tenant = TenantAggregate.CreateCompany(new TenantId(tenantId), "Test Tenant");
            _dbContext.Tenants.Add(tenant);
            await _dbContext.SaveChangesAsync();

            var customer = TestEntityBuilder.CreateCustomer(new TenantId(tenantId));
            customer.GetType().GetProperty(nameof(Customer.PhoneNumber))!
                .SetValue(customer, "+84-912-345-680");
            customer.GetType().GetProperty(nameof(Customer.Email))!
                .SetValue(customer, "migrate@example.com");
            _dbContext.Customers.Add(customer);
            await _dbContext.SaveChangesAsync();

            // Manually overwrite the stored values with plaintext to simulate pre-Wave 2 data
            // Keep connection open — closing destroys SQLite in-memory DB
            var connection = _dbContext.Database.GetDbConnection();
            bool wasOpen = connection.State == ConnectionState.Open;
            if (!wasOpen) await connection.OpenAsync();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE Customers SET PhoneNumber = @phone, Email = @email WHERE Id = @id";
                AddParameter(command, "@phone", "+84-912-345-680");
                AddParameter(command, "@email", "migrate@example.com");
                AddParameter(command, "@id", customer.Id);
                await command.ExecuteNonQueryAsync();
            }
            if (!wasOpen) await connection.CloseAsync();

            // Act
            var migrationService = new PiiDataMigrationService(_dbContext, _serviceProvider.GetRequiredService<ILogger<PiiDataMigrationService>>());
            await migrationService.MigrateAsync();

            // Assert: Read via EF Core should still return plaintext (decrypted)
            _dbContext.ChangeTracker.Clear();
            var savedCustomer = await _dbContext.Customers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == customer.Id);

            Assert.NotNull(savedCustomer);
            Assert.Equal("+84-912-345-680", savedCustomer.PhoneNumber);
            Assert.Equal("migrate@example.com", savedCustomer.Email);

            // Assert: Raw value is now encrypted
            wasOpen = connection.State == ConnectionState.Open;
            if (!wasOpen) await connection.OpenAsync();
            string? rawPhone;
            using (var verifyCommand = connection.CreateCommand())
            {
                verifyCommand.CommandText = "SELECT PhoneNumber FROM Customers WHERE Id = @id";
                AddParameter(verifyCommand, "@id", customer.Id);
                rawPhone = (string?)await verifyCommand.ExecuteScalarAsync();
            }
            if (!wasOpen) await connection.CloseAsync();

            Assert.NotEqual("+84-912-345-680", rawPhone);
        }

        [Fact(DisplayName = "W2-T7: EncryptedStringConverter round-trips a value")]
        public void EncryptedStringConverter_RoundTripsValue()
        {
            var protector = DataProtectionProviderAccessor.CreateProtector("Test.Purpose");
            var converter = new VanAn.CoreHub.Infrastructure.ValueConverters.EncryptedStringConverter(protector);
            const string plain = "plain-text-value";

            var encrypted = converter.ConvertToProvider(plain);
            var decrypted = converter.ConvertFromProvider(encrypted);

            Assert.NotEqual(plain, encrypted);
            Assert.Equal(plain, decrypted);
        }

        private static void AddParameter(DbCommand command, string name, object value)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            command.Parameters.Add(param);
        }
    }
}
