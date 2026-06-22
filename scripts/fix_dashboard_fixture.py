filepath = r'C:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Core.Tests\TestInfrastructure\DashboardTestFixture.cs'
with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

# Replace seeding call to pass context.CurrentTenantId
old = '            Context.SeedTestDataAsync(TestDataBuilder.CreateBasicScenario()).Wait();'
new = '            Context.SeedTestDataAsync(TestDataBuilder.CreateBasicScenario(Context.CurrentTenantId)).Wait();'

count = content.count(old)
content = content.replace(old, new)
with open(filepath, 'wb') as f:
    f.write(content.encode('utf-8'))
print(f'Replaced {count}x')
