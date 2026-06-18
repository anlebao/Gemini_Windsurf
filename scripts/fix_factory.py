filepath = r'C:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Core.Tests\TestInfrastructure\VanAnDbContextTestFactory.cs'
with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

old = '            return new TestContextScope(context, connection);'
new = '            return new TestContextScope(context, connection, tenantProvider);'

count = content.count(old)
content = content.replace(old, new)
with open(filepath, 'wb') as f:
    f.write(content.encode('utf-8'))
print(f'Replaced {count}x')
