"""
Fix VanAnDbContext.cs:
1. Add CurrentTenantIdString property (returns Guid as string, for SQLite TEXT comparison)
2. Change global query filter to use EF.Property<string> and compare with CurrentTenantIdString
   - column stores UUID as TEXT (after converter migration), EF.Property<string> matches it
"""

filepath = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\VanAnDbContext.cs'

with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

# 1. Add CurrentTenantIdString property after CurrentTenantId
old_prop = (
    '        // \U0001f6e1\ufe0f PUBLIC PROPERTY FOR EF Core Query Filter\r\n'
    '        public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Empty;\r\n'
)
new_prop = (
    '        // \U0001f6e1\ufe0f PUBLIC PROPERTY FOR EF Core Query Filter\r\n'
    '        public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Empty;\r\n'
    '\r\n'
    '        // Used by global query filter: TenantId column is stored as TEXT (UUID string)\r\n'
    '        // Both SQLite and Npgsql can compare TEXT columns with string parameters.\r\n'
    '        public string CurrentTenantIdString => CurrentTenantId.ToString();\r\n'
)

found1 = old_prop in content
if found1:
    content = content.replace(old_prop, new_prop)

# 2. Change efPropertyMethod to use string type
old_method = '                ?.MakeGenericMethod(typeof(Guid)) ?? throw new InvalidOperationException("Unable to resolve EF.Property<Guid> method");'
new_method = '                ?.MakeGenericMethod(typeof(string)) ?? throw new InvalidOperationException("Unable to resolve EF.Property<string> method");'
found2 = old_method in content
if found2:
    content = content.replace(old_method, new_method)

# 3. Change currentTenantIdExpr to use CurrentTenantIdString property
old_expr = (
    '            // Build CurrentTenantId property access expression on the captured context.\r\n'
    '            System.Linq.Expressions.MemberExpression currentTenantIdExpr = System.Linq.Expressions.Expression.Property(\r\n'
    '                System.Linq.Expressions.Expression.Constant(capturedContext),\r\n'
    '                nameof(CurrentTenantId));'
)
new_expr = (
    '            // Build CurrentTenantIdString property access expression on the captured context.\r\n'
    '            // The column is stored as TEXT (UUID string) — comparing string to string avoids\r\n'
    '            // SQLite IConvertible errors that occur when comparing Guid constants.\r\n'
    '            System.Linq.Expressions.MemberExpression currentTenantIdExpr = System.Linq.Expressions.Expression.Property(\r\n'
    '                System.Linq.Expressions.Expression.Constant(capturedContext),\r\n'
    '                nameof(CurrentTenantIdString));'
)
found3 = old_expr in content
if found3:
    content = content.replace(old_expr, new_expr)

# 4. Update comments about EF.Property<Guid> -> <string>
content = content.replace(
    '                // Build query filter expression: e => EF.Property<Guid>(e, "TenantId") == this.CurrentTenantId\r\n'
    '                // EF.Property<Guid> accesses the underlying column; CurrentTenantId is evaluated\r\n'
    '                // dynamically at query time via context property reference (generates @p0 parameter).',
    '                // Build query filter expression: e => EF.Property<string>(e, "TenantId") == this.CurrentTenantIdString\r\n'
    '                // EF.Property<string> accesses the underlying TEXT column (UUID stored as string);\r\n'
    '                // CurrentTenantIdString is evaluated dynamically at query time (generates @p0 parameter).'
)

with open(filepath, 'wb') as f:
    f.write(content.encode('utf-8'))

result_path = r'C:\VibeCoding\Gemini_Windsurf\scripts\dbctx_str_result.txt'
with open(result_path, 'w', encoding='utf-8') as log:
    log.write(f'Found old_prop: {found1}\n')
    log.write(f'Found old_method: {found2}\n')
    log.write(f'Found old_expr: {found3}\n')
    log.write('Done.\n')
