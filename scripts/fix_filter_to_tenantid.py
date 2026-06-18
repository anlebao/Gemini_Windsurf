"""
Fix ApplyMultiTenancyFilters to use e.TenantId == CurrentTenantIdValue.

Why this works:
- Parameter value = TenantId (model type) -> Sanitize<TenantId>(TenantId) -> TRUE (no Convert)
- Materialization: fromProvider(string_from_db) -> Sanitize<string>(string) -> TRUE (no Convert)

Both Sanitize<T> calls pass because T matches the value type.
"""

p = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\VanAnDbContext.cs'
with open(p, 'rb') as f:
    c = f.read().decode('utf-8-sig')

# Find the ApplyMultiTenancyFilters method body and replace
start_marker = '        // 🛡️ MULTI-TENANCY HELPER METHODS'
method_sig = 'private void ApplyMultiTenancyFilters(ModelBuilder modelBuilder)'
method_idx = c.find(method_sig)
if method_idx < 0:
    print('method not found')
    exit(1)

body_start = c.find('{', method_idx)
depth = 0
pos = body_start
while pos < len(c):
    if c[pos] == '{': depth += 1
    elif c[pos] == '}':
        depth -= 1
        if depth == 0:
            method_end = pos + 1
            break
    pos += 1

print(f'Method body: {body_start}:{method_end}')

new_body = '''\
{
            // Skip if tenant provider is null (for design-time or migrations)
            if (_tenantProvider == null)
            {
                return;
            }

            // Apply to all entities implementing IMustHaveTenant
            // (AccountingEntry excluded: special cross-tenant audit/reconciliation queries).
            IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType> entityTypes = modelBuilder.Model.GetEntityTypes()
                .Where(e => typeof(IMustHaveTenant).IsAssignableFrom(e.ClrType) && e.ClrType != typeof(CoreAccountingEntry));

            // Capture context so EF Core evaluates CurrentTenantIdValue at QUERY TIME.
            // Using TenantId (model type) as RHS ensures:
            //   Sanitize<TenantId>(TenantId_value) -> "value is TenantId" -> TRUE (no Convert.ChangeType)
            // When reading from DB:
            //   ConvertFromProvider(string_from_db) -> Sanitize<string>(string) -> "string is string" -> TRUE
            VanAnDbContext capturedContext = this;

            // Property: IMustHaveTenant.TenantId (CLR type: TenantId)
            System.Reflection.PropertyInfo tenantIdProp =
                typeof(IMustHaveTenant).GetProperty(nameof(IMustHaveTenant.TenantId))
                ?? throw new InvalidOperationException("IMustHaveTenant.TenantId property not found");

            // Property: VanAnDbContext.CurrentTenantIdValue (CLR type: TenantId)
            System.Reflection.PropertyInfo currentTenantIdProp =
                typeof(VanAnDbContext).GetProperty(nameof(CurrentTenantIdValue))
                ?? throw new InvalidOperationException("VanAnDbContext.CurrentTenantIdValue not found");

            System.Linq.Expressions.MemberExpression currentTenantIdExpr =
                System.Linq.Expressions.Expression.Property(
                    System.Linq.Expressions.Expression.Constant(capturedContext),
                    currentTenantIdProp);

            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in entityTypes)
            {
                System.Type clrType = entityType.ClrType;

                // e => ((IMustHaveTenant)e).TenantId == capturedContext.CurrentTenantIdValue
                // TenantId has ValueConverter<TenantId,string>: EF Core translates to
                //   WHERE "TenantId" = @p0   (with @p0 built via Sanitize<TenantId>(TenantId_val) -> OK)
                ParameterExpression parameter = System.Linq.Expressions.Expression.Parameter(clrType, "e");

                System.Linq.Expressions.MemberExpression entityTenantId =
                    System.Linq.Expressions.Expression.Property(
                        System.Linq.Expressions.Expression.Convert(parameter, typeof(IMustHaveTenant)),
                        tenantIdProp);

                System.Linq.Expressions.BinaryExpression comparison =
                    System.Linq.Expressions.Expression.Equal(entityTenantId, currentTenantIdExpr);

                LambdaExpression filterExpression =
                    System.Linq.Expressions.Expression.Lambda(comparison, parameter);

                modelBuilder.Entity(clrType).HasQueryFilter(filterExpression);
            }
        }'''

c = c[:body_start] + new_body + c[method_end:]
with open(p, 'wb') as f:
    f.write(c.encode('utf-8'))
print('Written')
