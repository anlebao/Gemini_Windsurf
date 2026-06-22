filepath = r'C:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Core.Tests\TestInfrastructure\TestDataBuilder.cs'
with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

# Add primaryTenantId parameter to CreateBasicScenario and CreateLargeScenario
old_basic = (
    '        // Static factory methods for common scenarios\r\n'
    '        public static TestDataBuilder CreateBasicScenario()\r\n'
    '        {\r\n'
    '            return new TestDataBuilder()\r\n'
    '                .WithShops(2)\r\n'
    '                .WithOrders(4)\r\n'
    '                .WithMixedSyncStatus();\r\n'
    '        }\r\n'
    '\r\n'
    '        public static TestDataBuilder CreateLargeScenario()\r\n'
    '        {\r\n'
    '            return new TestDataBuilder()\r\n'
    '                .WithShops(5)\r\n'
    '                .WithOrders(100)\r\n'
    '                .WithCustomers(50)\r\n'
    '                .WithMixedSyncStatus();\r\n'
    '        }'
)
new_basic = (
    '        // Static factory methods for common scenarios\r\n'
    '        /// <param name="primaryTenantId">\r\n'
    '        /// If provided, the first shop will use this TenantId so that seeded data\r\n'
    '        /// is visible through the global query filter in tests.\r\n'
    '        /// </param>\r\n'
    '        public static TestDataBuilder CreateBasicScenario(Guid? primaryTenantId = null)\r\n'
    '        {\r\n'
    '            return new TestDataBuilder()\r\n'
    '                .WithShops(2, primaryTenantId)\r\n'
    '                .WithOrders(4)\r\n'
    '                .WithMixedSyncStatus();\r\n'
    '        }\r\n'
    '\r\n'
    '        public static TestDataBuilder CreateLargeScenario(Guid? primaryTenantId = null)\r\n'
    '        {\r\n'
    '            return new TestDataBuilder()\r\n'
    '                .WithShops(5, primaryTenantId)\r\n'
    '                .WithOrders(100)\r\n'
    '                .WithCustomers(50)\r\n'
    '                .WithMixedSyncStatus();\r\n'
    '        }'
)

count = content.count(old_basic)
content = content.replace(old_basic, new_basic)
with open(filepath, 'wb') as f:
    f.write(content.encode('utf-8'))
print(f'Replaced {count}x')
