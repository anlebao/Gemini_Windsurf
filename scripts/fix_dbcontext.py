import sys

filepath = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\VanAnDbContext.cs'

with open(filepath, 'rb') as f:
    raw = f.read()

content = raw.decode('utf-8-sig')

# Check presence of key markers
print('Has efPropertyMethod:', 'efPropertyMethod' in content)
print('Has EF.Property<Guid> comment:', 'EF.Property<Guid>(e, "TenantId")' in content)

# Strategy: replace from "// Resolve EF.Property" line to end of foreach block
# Use markers that are unique in the file
START_MARKER = '            // Resolve EF.Property<Guid> MethodInfo safely'
END_MARKER = '                // Apply query filter\r\n                modelBuilder.Entity(clrType).HasQueryFilter(filterExpression);\r\n            }'

start_idx = content.find(START_MARKER)
end_idx = content.find(END_MARKER)

print(f'Start idx: {start_idx}, End idx: {end_idx}')

if start_idx == -1 or end_idx == -1:
    print('ERROR: markers not found')
    sys.exit(1)

end_idx += len(END_MARKER)

old_block = content[start_idx:end_idx]
print(f'Old block length: {len(old_block)}')
print('First 100 chars of old:', repr(old_block[:100]))

new_block = (
    '            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in entityTypes)\r\n'
    '            {\r\n'
    '                System.Type clrType = entityType.ClrType;\r\n'
    '\r\n'
    '                // Build query filter expression: e => e.TenantId.Value == currentTenantId\r\n'
    '                // Uses .Value (Guid primitive) instead of EF.Property<Guid> so SQLite and Npgsql\r\n'
    '                // both translate correctly.\r\n'
    '                ParameterExpression parameter = System.Linq.Expressions.Expression.Parameter(clrType, "e");\r\n'
    '                System.Linq.Expressions.MemberExpression tenantIdProp = System.Linq.Expressions.Expression.Property(\r\n'
    '                    parameter, nameof(IMustHaveTenant.TenantId));\r\n'
    '                System.Linq.Expressions.MemberExpression tenantIdValue = System.Linq.Expressions.Expression.Property(\r\n'
    '                    tenantIdProp, nameof(TenantId.Value));\r\n'
    '                System.Linq.Expressions.BinaryExpression comparison = System.Linq.Expressions.Expression.Equal(\r\n'
    '                    tenantIdValue,\r\n'
    '                    System.Linq.Expressions.Expression.Constant(currentTenantId, typeof(Guid))\r\n'
    '                );\r\n'
    '                LambdaExpression filterExpression = System.Linq.Expressions.Expression.Lambda(comparison, parameter);\r\n'
    '\r\n'
    '                // Apply query filter\r\n'
    '                modelBuilder.Entity(clrType).HasQueryFilter(filterExpression);\r\n'
    '            }'
)

new_content = content[:start_idx] + new_block + content[end_idx:]

with open(filepath, 'wb') as f:
    f.write(new_content.encode('utf-8'))

print('File written successfully.')
print('New block preview:')
print(new_content[start_idx:start_idx+200])
