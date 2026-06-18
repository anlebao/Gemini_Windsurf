filepath = r'C:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Core.Tests\IntegrationTestBase.cs'
with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

# 1. Fix SetupBasicTestDataAsync: pass Context.CurrentTenantId to seed
old1 = (
    '        protected async Task SetupBasicTestDataAsync()\r\n'
    '        {\r\n'
    '            await CreateContextAsync();\r\n'
    '            await SeedTestDataAsync(TestDataBuilder.CreateBasicScenario());\r\n'
    '        }'
)
new1 = (
    '        protected async Task SetupBasicTestDataAsync()\r\n'
    '        {\r\n'
    '            await CreateContextAsync();\r\n'
    '            await SeedTestDataAsync(TestDataBuilder.CreateBasicScenario(Context.CurrentTenantId));\r\n'
    '        }'
)

# 2. Fix SetupLargeTestDataAsync
old2 = (
    '        protected async Task SetupLargeTestDataAsync()\r\n'
    '        {\r\n'
    '            await CreateContextAsync();\r\n'
    '            await SeedTestDataAsync(TestDataBuilder.CreateLargeScenario());\r\n'
    '        }'
)
new2 = (
    '        protected async Task SetupLargeTestDataAsync()\r\n'
    '        {\r\n'
    '            await CreateContextAsync();\r\n'
    '            await SeedTestDataAsync(TestDataBuilder.CreateLargeScenario(Context.CurrentTenantId));\r\n'
    '        }'
)

# 3. Add SetActiveTenant helper and ActiveTenantId getter after Dispose
old3 = (
    '        // Legacy method for backward compatibility\r\n'
    '        protected virtual async Task SetupAsync()\r\n'
    '        {\r\n'
    '            await SetupBasicTestDataAsync();\r\n'
    '        }\r\n'
    '    }\r\n'
    '}'
)
new3 = (
    '        /// <summary>\r\n'
    '        /// Gets the active Tenant ID from the test context\'s tenant provider.\r\n'
    '        /// Use this as the shopId in kitchen tests so data seeded with this tenant\r\n'
    '        /// is visible through the global multi-tenancy query filter.\r\n'
    '        /// </summary>\r\n'
    '        protected Guid ActiveTenantId => ContextScope?.ActiveTenantId ?? Guid.Empty;\r\n'
    '\r\n'
    '        /// <summary>\r\n'
    '        /// Changes the active tenant for this test context.\r\n'
    '        /// Data must be seeded AFTER calling this for the global filter to include it.\r\n'
    '        /// </summary>\r\n'
    '        protected void SetActiveTenant(Guid tenantId)\r\n'
    '        {\r\n'
    '            ContextScope?.TenantProvider?.SetTenant(tenantId);\r\n'
    '        }\r\n'
    '\r\n'
    '        // Legacy method for backward compatibility\r\n'
    '        protected virtual async Task SetupAsync()\r\n'
    '        {\r\n'
    '            await SetupBasicTestDataAsync();\r\n'
    '        }\r\n'
    '    }\r\n'
    '}'
)

c1 = content.count(old1)
c2 = content.count(old2)
c3 = content.count(old3)

content = content.replace(old1, new1)
content = content.replace(old2, new2)
content = content.replace(old3, new3)

with open(filepath, 'wb') as f:
    f.write(content.encode('utf-8'))
print(f'Replaced: basic={c1}, large={c2}, helper={c3}')
