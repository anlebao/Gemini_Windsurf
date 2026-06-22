p = r'C:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Core.Tests\TestInfrastructure\TestDataBuilder.cs'
with open(p, 'rb') as f:
    c = f.read().decode('utf-8-sig')

old = (
    '                // Clear existing data\r\n'
    '                context.Orders.RemoveRange(context.Orders);\r\n'
    '                context.Shops.RemoveRange(context.Shops);\r\n'
    '                context.Customers.RemoveRange(context.Customers);\r\n'
    '                _ = await context.SaveChangesAsync();'
)

new = (
    '                // Clear existing data via raw SQL to avoid entity materialization.\r\n'
    '                // Materializing entities with custom value converters can throw on SQLite TEXT columns\r\n'
    '                // when EF Core internal Sanitize<T> calls Convert.ChangeType for non-IConvertible types.\r\n'
    '                _ = await context.Database.ExecuteSqlRawAsync("DELETE FROM OrderItems");\r\n'
    '                _ = await context.Database.ExecuteSqlRawAsync("DELETE FROM Orders");\r\n'
    '                _ = await context.Database.ExecuteSqlRawAsync("DELETE FROM Customers");\r\n'
    '                _ = await context.Database.ExecuteSqlRawAsync("DELETE FROM Shops");'
)

count = c.count(old)
print(f'Found: {count}')
if count:
    c = c.replace(old, new)
    with open(p, 'wb') as f:
        f.write(c.encode('utf-8'))
    print('Written')
else:
    # Debug: show context
    idx = c.find('RemoveRange(context.Orders)')
    print('Context:', repr(c[max(0, idx-100):idx+200]))
