"""
Revert global filter in VanAnDbContext to use EF.Property<Guid>(e, "TenantId")
instead of e.TenantId.Value — because SQLite provider cannot translate .Value
property access on ValueConverter-backed properties in expression trees.

Now that OrderConfiguration and InvoiceItemConfiguration have TenantId converters,
EF.Property<Guid> will work correctly on all entities.
"""

filepath = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\VanAnDbContext.cs'

with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

# Find the foreach block with .Value approach and replace
old_block = (
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

new_block = (
    '            // Resolve EF.Property<Guid> MethodInfo safely with explicit parameter types to avoid\r\n'
    '            // ambiguous match between different EF.Property<T> overloads.\r\n'
    '            // NOTE: EF.Property<Guid>(e, "TenantId") works because all IMustHaveTenant entities\r\n'
    '            // now have TenantId HasConversion configured in their IEntityTypeConfiguration.\r\n'
    '            System.Reflection.MethodInfo efPropertyMethod = typeof(EF)\r\n'
    '                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)\r\n'
    '                .Where(m => m.Name == "Property" && m.IsGenericMethod && m.GetParameters().Length == 2)\r\n'
    '                .Select(m => new { Method = m, Parameters = m.GetParameters() })\r\n'
    '                .Where(x => x.Parameters[0].ParameterType == typeof(object) && x.Parameters[1].ParameterType == typeof(string))\r\n'
    '                .Select(x => x.Method)\r\n'
    '                .FirstOrDefault()\r\n'
    '                ?.MakeGenericMethod(typeof(Guid)) ?? throw new InvalidOperationException("Unable to resolve EF.Property<Guid> method");\r\n'
    '\r\n'
    '            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in entityTypes)\r\n'
    '            {\r\n'
    '                System.Type clrType = entityType.ClrType;\r\n'
    '\r\n'
    '                // Build query filter expression: e => EF.Property<Guid>(e, "TenantId") == currentTenantId\r\n'
    '                // EF.Property<Guid> accesses the underlying Guid column directly, bypassing\r\n'
    '                // the TenantId value object — supported by both SQLite and Npgsql providers.\r\n'
    '                ParameterExpression parameter = System.Linq.Expressions.Expression.Parameter(clrType, "e");\r\n'
    '                System.Linq.Expressions.MethodCallExpression propertyCall = System.Linq.Expressions.Expression.Call(\r\n'
    '                    null, // static method\r\n'
    '                    efPropertyMethod,\r\n'
    '                    System.Linq.Expressions.Expression.Convert(parameter, typeof(object)),\r\n'
    '                    System.Linq.Expressions.Expression.Constant("TenantId", typeof(string))\r\n'
    '                );\r\n'
    '                System.Linq.Expressions.BinaryExpression comparison = System.Linq.Expressions.Expression.Equal(\r\n'
    '                    propertyCall,\r\n'
    '                    System.Linq.Expressions.Expression.Constant(currentTenantId, typeof(Guid))\r\n'
    '                );\r\n'
    '                LambdaExpression filterExpression = System.Linq.Expressions.Expression.Lambda(comparison, parameter);\r\n'
    '\r\n'
    '                // Apply query filter\r\n'
    '                modelBuilder.Entity(clrType).HasQueryFilter(filterExpression);\r\n'
    '            }'
)

found = old_block in content
with open('C:\\VibeCoding\\Gemini_Windsurf\\scripts\\v2_result.txt', 'w') as log:
    log.write(f'Old block found: {found}\n')

if found:
    content = content.replace(old_block, new_block)
    with open(filepath, 'wb') as f:
        f.write(content.encode('utf-8'))
    with open('C:\\VibeCoding\\Gemini_Windsurf\\scripts\\v2_result.txt', 'a') as log:
        log.write('Replaced successfully.\n')
else:
    with open('C:\\VibeCoding\\Gemini_Windsurf\\scripts\\v2_result.txt', 'a') as log:
        log.write(f'NOT FOUND. Searching partial...\n')
        idx = content.find('foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in entityTypes)')
        log.write(f'foreach idx: {idx}\n')
        if idx >= 0:
            log.write(f'Context around foreach: {repr(content[idx-200:idx+100])}\n')
