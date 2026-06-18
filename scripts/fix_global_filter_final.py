"""
Rewrite ApplyMultiTenancyFilters completely.
The VanAnDbContext.cs has mixed CRLF/LF so we rewrite the entire method.
"""

p = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\VanAnDbContext.cs'
with open(p, 'rb') as f:
    raw = f.read()
c = raw.decode('utf-8-sig')

# Find method start and end
START_MARKER = '        // 🛡️ MULTI-TENANCY HELPER METHODS'
# Method ends at the closing brace at same indent level
start_idx = c.find(START_MARKER)
if start_idx < 0:
    # try without emoji
    start_idx = c.find('        // MULTI-TENANCY HELPER METHODS')
if start_idx < 0:
    print('START not found')
    exit(1)

# Find "private void ApplyMultiTenancyFilters" after start
method_start = c.find('private void ApplyMultiTenancyFilters', start_idx)
if method_start < 0:
    print('method not found')
    exit(1)

# Find the body: first { after method signature
body_start = c.find('{', method_start)
# Find matching }
depth = 0
pos = body_start
while pos < len(c):
    if c[pos] == '{':
        depth += 1
    elif c[pos] == '}':
        depth -= 1
        if depth == 0:
            method_end = pos + 1
            break
    pos += 1

print(f'Method spans {method_start}:{method_end}, body {body_start}:{method_end}')
print(f'First 200 chars of method: {repr(c[method_start:method_start+200])}')

# Build replacement (just the method body)
new_body = '''\
{
            // Skip if tenant provider is null (for design-time or migrations)
            if (_tenantProvider == null)
            {
                return;
            }

            // Apply to all entities that implement IMustHaveTenant
            // AccountingEntry excluded: special cross-tenant queries (audit, reconciliation).
            IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType> entityTypes = modelBuilder.Model.GetEntityTypes()
                .Where(e => typeof(IMustHaveTenant).IsAssignableFrom(e.ClrType) && e.ClrType != typeof(CoreAccountingEntry));

            // Capture context so that CurrentTenantIdString is evaluated at QUERY TIME
            // (generates SQL parameter @p0 = currentTenantId.ToString()).
            VanAnDbContext capturedContext = this;

            // Resolve EF.Property<string> MethodInfo.
            // We use EF.Property<string>(e, "TenantId") to compare the raw TEXT column
            // as a string, which avoids the TenantId ValueConverter's Sanitize<TenantId>
            // wrapper that calls Convert.ChangeType and fails for non-IConvertible types.
            System.Reflection.MethodInfo efPropertyMethod = typeof(EF)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(m => m.Name == "Property" && m.IsGenericMethod && m.GetParameters().Length == 2)
                .Select(m => new { Method = m, Parameters = m.GetParameters() })
                .Where(x => x.Parameters[0].ParameterType == typeof(object) && x.Parameters[1].ParameterType == typeof(string))
                .Select(x => x.Method)
                .FirstOrDefault()
                ?.MakeGenericMethod(typeof(string)) ?? throw new InvalidOperationException("Unable to resolve EF.Property<string> method");

            // CurrentTenantIdString is a string property — its TypeMapping is a plain StringTypeMapping.
            // Comparing string to string avoids Sanitize<TenantId>(string) errors.
            System.Reflection.PropertyInfo currentTenantStrProp =
                typeof(VanAnDbContext).GetProperty(nameof(CurrentTenantIdString))
                ?? throw new InvalidOperationException("VanAnDbContext.CurrentTenantIdString not found");

            System.Linq.Expressions.MemberExpression currentTenantStrExpr =
                System.Linq.Expressions.Expression.Property(
                    System.Linq.Expressions.Expression.Constant(capturedContext),
                    currentTenantStrProp);

            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in entityTypes)
            {
                System.Type clrType = entityType.ClrType;

                // e => EF.Property<string>(e, "TenantId") == capturedContext.CurrentTenantIdString
                // Both sides are plain strings, so no IConvertible or Sanitize issue.
                ParameterExpression parameter = System.Linq.Expressions.Expression.Parameter(clrType, "e");

                System.Linq.Expressions.MethodCallExpression propertyCall =
                    System.Linq.Expressions.Expression.Call(
                        null,
                        efPropertyMethod,
                        System.Linq.Expressions.Expression.Convert(parameter, typeof(object)),
                        System.Linq.Expressions.Expression.Constant("TenantId", typeof(string)));

                System.Linq.Expressions.BinaryExpression comparison =
                    System.Linq.Expressions.Expression.Equal(propertyCall, currentTenantStrExpr);

                LambdaExpression filterExpression =
                    System.Linq.Expressions.Expression.Lambda(comparison, parameter);

                modelBuilder.Entity(clrType).HasQueryFilter(filterExpression);
            }
        }'''

# Replace just the body (from body_start inclusive to method_end exclusive)
c = c[:body_start] + new_body + c[method_end:]

with open(p, 'wb') as f:
    f.write(c.encode('utf-8'))
print('Written')
