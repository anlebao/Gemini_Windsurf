"""
Fix ApplyMultiTenancyFilters() in VanAnDbContext:
Replace Expression.Constant(currentTenantId, typeof(Guid)) with a dynamic property reference
to this.CurrentTenantId — so EF Core generates parameterized SQL (@p0) instead of
a Guid literal, which SQLite cannot handle (IConvertible error).

The fix uses Expression.Property(closedOverContext, "CurrentTenantId") where closedOverContext
is the DbContext instance captured via closure — standard EF Core pattern for dynamic filters.
"""

filepath = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\VanAnDbContext.cs'

with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

# Find and replace the foreach block
old_block = (
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

new_block = (
    '            // Build a closure over "this" context to capture CurrentTenantId dynamically.\r\n'
    '            // Using a property reference instead of Expression.Constant(Guid) avoids the\r\n'
    '            // SQLite IConvertible error (SQLite cannot create SQL literals for Guid type).\r\n'
    '            // EF Core will generate a parameterized query (@p0) evaluated at query time.\r\n'
    '            VanAnDbContext capturedContext = this;\r\n'
    '\r\n'
    '            // Resolve EF.Property<Guid> MethodInfo safely.\r\n'
    '            System.Reflection.MethodInfo efPropertyMethod = typeof(EF)\r\n'
    '                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)\r\n'
    '                .Where(m => m.Name == "Property" && m.IsGenericMethod && m.GetParameters().Length == 2)\r\n'
    '                .Select(m => new { Method = m, Parameters = m.GetParameters() })\r\n'
    '                .Where(x => x.Parameters[0].ParameterType == typeof(object) && x.Parameters[1].ParameterType == typeof(string))\r\n'
    '                .Select(x => x.Method)\r\n'
    '                .FirstOrDefault()\r\n'
    '                ?.MakeGenericMethod(typeof(Guid)) ?? throw new InvalidOperationException("Unable to resolve EF.Property<Guid> method");\r\n'
    '\r\n'
    '            // Build CurrentTenantId property access expression on the captured context.\r\n'
    '            System.Linq.Expressions.MemberExpression currentTenantIdExpr = System.Linq.Expressions.Expression.Property(\r\n'
    '                System.Linq.Expressions.Expression.Constant(capturedContext),\r\n'
    '                nameof(CurrentTenantId));\r\n'
    '\r\n'
    '            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in entityTypes)\r\n'
    '            {\r\n'
    '                System.Type clrType = entityType.ClrType;\r\n'
    '\r\n'
    '                // Build query filter expression: e => EF.Property<Guid>(e, "TenantId") == this.CurrentTenantId\r\n'
    '                // EF.Property<Guid> accesses the underlying column; CurrentTenantId is evaluated\r\n'
    '                // dynamically at query time via context property reference (generates @p0 parameter).\r\n'
    '                ParameterExpression parameter = System.Linq.Expressions.Expression.Parameter(clrType, "e");\r\n'
    '                System.Linq.Expressions.MethodCallExpression propertyCall = System.Linq.Expressions.Expression.Call(\r\n'
    '                    null, // static method\r\n'
    '                    efPropertyMethod,\r\n'
    '                    System.Linq.Expressions.Expression.Convert(parameter, typeof(object)),\r\n'
    '                    System.Linq.Expressions.Expression.Constant("TenantId", typeof(string))\r\n'
    '                );\r\n'
    '                System.Linq.Expressions.BinaryExpression comparison = System.Linq.Expressions.Expression.Equal(\r\n'
    '                    propertyCall,\r\n'
    '                    currentTenantIdExpr\r\n'
    '                );\r\n'
    '                LambdaExpression filterExpression = System.Linq.Expressions.Expression.Lambda(comparison, parameter);\r\n'
    '\r\n'
    '                // Apply query filter\r\n'
    '                modelBuilder.Entity(clrType).HasQueryFilter(filterExpression);\r\n'
    '            }'
)

found = old_block in content
with open('C:\\VibeCoding\\Gemini_Windsurf\\scripts\\v3_result.txt', 'w') as log:
    log.write(f'Old block found: {found}\n')

if found:
    content = content.replace(old_block, new_block)
    with open(filepath, 'wb') as f:
        f.write(content.encode('utf-8'))
    with open('C:\\VibeCoding\\Gemini_Windsurf\\scripts\\v3_result.txt', 'a') as log:
        log.write('Written successfully.\n')
else:
    # Debug
    idx = content.find('Resolve EF.Property')
    with open('C:\\VibeCoding\\Gemini_Windsurf\\scripts\\v3_result.txt', 'a') as log:
        log.write(f'First Resolve EF.Property idx: {idx}\n')
        if idx >= 0:
            log.write(repr(content[idx:idx+200]) + '\n')
