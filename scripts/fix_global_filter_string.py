"""
Fix ApplyMultiTenancyFilters to use string comparison with Expression.Constant.

Why Expression.Constant works for string but NOT for Guid:
- Expression.Constant("guid-string", typeof(string)) -> EF Core creates SQL literal or parameter
  TypeMapping = StringTypeMapping (simple string, no custom converter) -> no Sanitize issue
- Expression.Constant(guid, typeof(Guid)) -> TypeMapping = GuidTypeMapping -> Sanitize<Guid> issues

Using Expression.Constant(currentTenantIdStr, typeof(string)) with EF.Property<string>(e, "TenantId"):
- Column TypeMapping = TenantId->string (our converter)
- Parameter TypeMapping = StringTypeMapping (from constant typeof(string))
- EF Core compares two strings: column_value == @p0_string
- No Sanitize for string type (string is string -> passes immediately)

This is cleaner and avoids the "this.CurrentTenantIdValue" property reference approach
that was causing Sanitize<TenantId>(string) when creating the parameter.
"""

p = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\VanAnDbContext.cs'
with open(p, 'rb') as f:
    raw = f.read()
c = raw.decode('utf-8-sig')

old_method = (
    '        // 🛡️ MULTI-TENANCY HELPER METHODS\r\n'
    '        private void ApplyMultiTenancyFilters(ModelBuilder modelBuilder)\r\n'
    '        {\r\n'
    '            // Skip if tenant provider is null (for design-time or migrations)\r\n'
    '            if (_tenantProvider == null)\r\n'
    '            {\r\n'
    '                return;\r\n'
    '            }\r\n'
    '\r\n'
    '            // Apply to all entities that implement IMustHaveTenant (AccountingEntry excluded:\r\n'
    '            // special case for cross-tenant queries, audit/history, reconciliation).\r\n'
    '            IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType> entityTypes = modelBuilder.Model.GetEntityTypes()\r\n'
    '                .Where(e => typeof(IMustHaveTenant).IsAssignableFrom(e.ClrType) && e.ClrType != typeof(CoreAccountingEntry));\r\n'
    '\r\n'
    '            // Capture context so EF Core evaluates CurrentTenantIdValue at QUERY TIME,\r\n'
    '            // generating a SQL parameter (@p0) instead of a hard-coded literal.\r\n'
    '            VanAnDbContext capturedContext = this;\r\n'
    '\r\n'
    '            // Property: IMustHaveTenant.TenantId (CLR type: TenantId, converter: TenantId -> string)\r\n'
    '            System.Reflection.PropertyInfo tenantIdProp =\r\n'
    '                typeof(IMustHaveTenant).GetProperty(nameof(IMustHaveTenant.TenantId))\r\n'
    '                ?? throw new InvalidOperationException("IMustHaveTenant.TenantId property not found");\r\n'
    '\r\n'
    '            // Property: VanAnDbContext.CurrentTenantIdValue (CLR type: TenantId)\r\n'
    '            System.Reflection.PropertyInfo currentTenantIdProp =\r\n'
    '                typeof(VanAnDbContext).GetProperty(nameof(CurrentTenantIdValue))\r\n'
    '                ?? throw new InvalidOperationException("VanAnDbContext.CurrentTenantIdValue property not found");\r\n'
    '\r\n'
    '            System.Linq.Expressions.MemberExpression currentTenantIdExpr =\r\n'
    '                System.Linq.Expressions.Expression.Property(\r\n'
    '                    System.Linq.Expressions.Expression.Constant(capturedContext),\r\n'
    '                    currentTenantIdProp);\r\n'
    '\r\n'
    '            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in entityTypes)\r\n'
    '            {\r\n'
    '                System.Type clrType = entityType.ClrType;\r\n'
    '\r\n'
    '                // Build:  e => ((IMustHaveTenant)e).TenantId == capturedContext.CurrentTenantIdValue\r\n'
    '                // TenantId has ValueConverter<TenantId,string>, so EF Core translates this to:\r\n'
    '                //   WHERE "TenantId" = @p0   (with @p0 = tenantId.Value.ToString())\r\n'
    '                ParameterExpression parameter = System.Linq.Expressions.Expression.Parameter(clrType, "e");\r\n'
    '\r\n'
    '                System.Linq.Expressions.MemberExpression entityTenantId =\r\n'
    '                    System.Linq.Expressions.Expression.Property(\r\n'
    '                        System.Linq.Expressions.Expression.Convert(parameter, typeof(IMustHaveTenant)),\r\n'
    '                        tenantIdProp);\r\n'
    '\r\n'
    '                System.Linq.Expressions.BinaryExpression comparison =\r\n'
    '                    System.Linq.Expressions.Expression.Equal(entityTenantId, currentTenantIdExpr);\r\n'
    '\r\n'
    '                LambdaExpression filterExpression =\r\n'
    '                    System.Linq.Expressions.Expression.Lambda(comparison, parameter);\r\n'
    '\r\n'
    '                modelBuilder.Entity(clrType).HasQueryFilter(filterExpression);\r\n'
    '            }\r\n'
    '        }'
)

new_method = (
    '        // 🛡️ MULTI-TENANCY HELPER METHODS\r\n'
    '        private void ApplyMultiTenancyFilters(ModelBuilder modelBuilder)\r\n'
    '        {\r\n'
    '            // Skip if tenant provider is null (for design-time or migrations)\r\n'
    '            if (_tenantProvider == null)\r\n'
    '            {\r\n'
    '                return;\r\n'
    '            }\r\n'
    '\r\n'
    '            // Apply to all entities that implement IMustHaveTenant (AccountingEntry excluded:\r\n'
    '            // special case for cross-tenant queries, audit/history, reconciliation).\r\n'
    '            IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType> entityTypes = modelBuilder.Model.GetEntityTypes()\r\n'
    '                .Where(e => typeof(IMustHaveTenant).IsAssignableFrom(e.ClrType) && e.ClrType != typeof(CoreAccountingEntry));\r\n'
    '\r\n'
    '            // Capture context so the filter lambda reads CurrentTenantIdString at QUERY TIME\r\n'
    '            // (generates a parameterized SQL query @p0 = currentTenant.ToString()).\r\n'
    '            VanAnDbContext capturedContext = this;\r\n'
    '\r\n'
    '            // Resolve EF.Property<string> MethodInfo safely.\r\n'
    '            // We use EF.Property<string>(e, "TenantId") to read the TEXT column directly as a\r\n'
    '            // string, bypassing the TenantId value-object TypeMapping and its Sanitize wrapper.\r\n'
    '            // This avoids Sanitize<TenantId>(string) which would call Convert.ChangeType and fail.\r\n'
    '            System.Reflection.MethodInfo efPropertyMethod = typeof(EF)\r\n'
    '                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)\r\n'
    '                .Where(m => m.Name == "Property" && m.IsGenericMethod && m.GetParameters().Length == 2)\r\n'
    '                .Select(m => new { Method = m, Parameters = m.GetParameters() })\r\n'
    '                .Where(x => x.Parameters[0].ParameterType == typeof(object) && x.Parameters[1].ParameterType == typeof(string))\r\n'
    '                .Select(x => x.Method)\r\n'
    '                .FirstOrDefault()\r\n'
    '                ?.MakeGenericMethod(typeof(string)) ?? throw new InvalidOperationException("Unable to resolve EF.Property<string> method");\r\n'
    '\r\n'
    '            // Access CurrentTenantIdString property dynamically so EF Core evaluates it at\r\n'
    '            // query time, not at model-build time. This generates SQL parameter @p0.\r\n'
    '            System.Reflection.PropertyInfo currentTenantStrProp =\r\n'
    '                typeof(VanAnDbContext).GetProperty(nameof(CurrentTenantIdString))\r\n'
    '                ?? throw new InvalidOperationException("VanAnDbContext.CurrentTenantIdString property not found");\r\n'
    '\r\n'
    '            System.Linq.Expressions.MemberExpression currentTenantStrExpr =\r\n'
    '                System.Linq.Expressions.Expression.Property(\r\n'
    '                    System.Linq.Expressions.Expression.Constant(capturedContext),\r\n'
    '                    currentTenantStrProp);\r\n'
    '\r\n'
    '            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in entityTypes)\r\n'
    '            {\r\n'
    '                System.Type clrType = entityType.ClrType;\r\n'
    '\r\n'
    '                // Filter: e => EF.Property<string>(e, "TenantId") == this.CurrentTenantIdString\r\n'
    '                //\r\n'
    '                // EF.Property<string> reads the raw TEXT column value (not through TenantId converter),\r\n'
    '                // and CurrentTenantIdString is a plain string parameter — no Sanitize<TenantId> is triggered.\r\n'
    '                // EF Core generates:  WHERE "TenantId" = @p0  (both sides are strings).\r\n'
    '                ParameterExpression parameter = System.Linq.Expressions.Expression.Parameter(clrType, "e");\r\n'
    '\r\n'
    '                System.Linq.Expressions.MethodCallExpression propertyCall =\r\n'
    '                    System.Linq.Expressions.Expression.Call(\r\n'
    '                        null,\r\n'
    '                        efPropertyMethod,\r\n'
    '                        System.Linq.Expressions.Expression.Convert(parameter, typeof(object)),\r\n'
    '                        System.Linq.Expressions.Expression.Constant("TenantId", typeof(string)));\r\n'
    '\r\n'
    '                System.Linq.Expressions.BinaryExpression comparison =\r\n'
    '                    System.Linq.Expressions.Expression.Equal(propertyCall, currentTenantStrExpr);\r\n'
    '\r\n'
    '                LambdaExpression filterExpression =\r\n'
    '                    System.Linq.Expressions.Expression.Lambda(comparison, parameter);\r\n'
    '\r\n'
    '                modelBuilder.Entity(clrType).HasQueryFilter(filterExpression);\r\n'
    '            }\r\n'
    '        }'
)

count = c.count(old_method)
if count == 0:
    # debug
    idx = c.find('private void ApplyMultiTenancyFilters')
    with open(r'C:\VibeCoding\Gemini_Windsurf\scripts\filter_dbg.txt', 'w', encoding='utf-8') as f:
        f.write(repr(c[idx:idx+300]))
    print('NOT FOUND, wrote debug')
else:
    c = c.replace(old_method, new_method)
    with open(p, 'wb') as f:
        f.write(c.encode('utf-8'))
    print(f'Replaced {count}x')
