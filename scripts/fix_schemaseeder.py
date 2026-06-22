filepath = r'C:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Core.Tests\TestInfrastructure\SchemaSyncEngine.cs'
with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

old = (
    '        public static async Task<bool> SeedTestDataAsync(this VanAnDbContext context, TestDataBuilder builder = null!)\r\n'
    '        {\r\n'
    '            try\r\n'
    '            {\r\n'
    '                builder ??= TestDataBuilder.CreateBasicScenario();\r\n'
    '                await builder.BuildAsync(context);\r\n'
    '                return true;\r\n'
    '            }\r\n'
    '            catch (Exception ex)\r\n'
    '            {\r\n'
    '                // Log error if logger available\r\n'
    '                Console.WriteLine($"Test data seeding failed: {ex.Message}");\r\n'
    '                return false;\r\n'
    '            }\r\n'
    '        }'
)
new = (
    '        public static async Task<bool> SeedTestDataAsync(this VanAnDbContext context, TestDataBuilder builder = null!)\r\n'
    '        {\r\n'
    '            try\r\n'
    '            {\r\n'
    '                // Seed data using the context\'s current tenant so data is visible through\r\n'
    '                // the global query filter (which filters by CurrentTenantId).\r\n'
    '                Guid primaryTenantId = context.CurrentTenantId;\r\n'
    '                builder ??= TestDataBuilder.CreateBasicScenario(primaryTenantId);\r\n'
    '                await builder.BuildAsync(context);\r\n'
    '                return true;\r\n'
    '            }\r\n'
    '            catch (Exception ex)\r\n'
    '            {\r\n'
    '                // Log error if logger available\r\n'
    '                Console.WriteLine($"Test data seeding failed: {ex.Message}");\r\n'
    '                return false;\r\n'
    '            }\r\n'
    '        }'
)

count = content.count(old)
content = content.replace(old, new)
with open(filepath, 'wb') as f:
    f.write(content.encode('utf-8'))
print(f'Replaced {count}x')
