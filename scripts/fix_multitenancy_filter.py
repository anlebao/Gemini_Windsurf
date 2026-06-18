import re

p = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\VanAnDbContext.cs'
with open(p, 'rb') as f:
    raw = f.read()
c = raw.decode('utf-8-sig')

# Replace the entire ApplyMultiTenancyFilters method body
# Use regex to find and replace
pattern = re.compile(
    r'(        // 🛡️ MULTI-TENANCY HELPER METHODS\r\n'
    r'        private void ApplyMultiTenancyFilters\(ModelBuilder modelBuilder\)\r\n'
    r'        \{).*?(        \})',
    re.DOTALL
)

new_method = r'''\1
            // Skip if tenant provider is null (for design-time or migrations)
            if (_tenantProvider == null)
            {
                return;
            }

            // Apply to all entities that implement IMustHaveTenant (AccountingEntry excluded:
            // special case for cross-tenant queries, audit/history, reconciliation).
            IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType> entityTypes = modelBuilder.Model.GetEntityTypes()
                .Where(e => typeof(IMustHaveTenant).IsAssignableFrom(e.ClrType) && e.ClrType != typeof(CoreAccountingEntry));

            // Capture context so EF Core evaluates CurrentTenantIdValue at QUERY TIME,
            // generating a SQL parameter (@p0) instead of a hard-coded literal.
            VanAnDbContext capturedContext = this;

            // Property: IMustHaveTenant.TenantId (CLR type: TenantId, converter: TenantId -> string)
            System.Reflection.PropertyInfo tenantIdProp =
                typeof(IMustHaveTenant).GetProperty(nameof(IMustHaveTenant.TenantId))
                ?? throw new InvalidOperationException("IMustHaveTenant.TenantId property not found");

            // Property: VanAnDbContext.CurrentTenantIdValue (CLR type: TenantId)
            System.Reflection.PropertyInfo currentTenantIdProp =
                typeof(VanAnDbContext).GetProperty(nameof(CurrentTenantIdValue))
                ?? throw new InvalidOperationException("VanAnDbContext.CurrentTenantIdValue property not found");

            System.Linq.Expressions.MemberExpression currentTenantIdExpr =
                System.Linq.Expressions.Expression.Property(
                    System.Linq.Expressions.Expression.Constant(capturedContext),
                    currentTenantIdProp);

            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in entityTypes)
            {
                System.Type clrType = entityType.ClrType;

                // Build:  e => ((IMustHaveTenant)e).TenantId == capturedContext.CurrentTenantIdValue
                // TenantId has ValueConverter<TenantId,string>, so EF Core translates this to:
                //   WHERE "TenantId" = @p0   (with @p0 = tenantId.Value.ToString())
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
\2'''

result, n = pattern.subn(new_method, c)
print(f'Replacements: {n}')

if n > 0:
    with open(p, 'wb') as f:
        f.write(result.encode('utf-8'))
    print('Written')
else:
    # debug
    idx = c.find('private void ApplyMultiTenancyFilters')
    with open(r'C:\VibeCoding\Gemini_Windsurf\scripts\dbctx_dbg.txt', 'w', encoding='utf-8') as f:
        f.write(repr(c[max(0,idx-200):idx+500]))
    print('Not found - wrote debug')
