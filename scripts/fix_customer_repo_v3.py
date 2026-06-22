"""
Fix CustomerRepository.cs:
Remove explicit TenantId filters from WHERE clauses — the global query filter in
VanAnDbContext already enforces multi-tenancy for all Customer queries.
Duplicate TenantId filtering causes IConvertible errors on SQLite because
EF Core SQLite cannot generate SQL literals for Guid type.

The global filter applies: EF.Property<Guid>(e, "TenantId") == currentTenantId
so explicit per-query filtering is redundant and causes issues.
"""

filepath = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Repositories\CustomerRepository.cs'

with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')
has_crlf = b'\r\n' in raw
sep = '\r\n' if has_crlf else '\n'

replacements = []

# GetByIdAsync: remove EF.Property TenantId line
replacements.append((
    f'                .Where(c => c.Id == id &&{sep}'
    f'                           EF.Property<Guid>(c, "TenantId") == tenantId &&{sep}'
    f'                           !c.IsDeleted)',
    f'                .Where(c => c.Id == id && !c.IsDeleted)'
))

# GetByDeviceIdAsync: remove EF.Property TenantId line
replacements.append((
    f'                .Where(c => c.DeviceId == deviceId &&{sep}'
    f'                           EF.Property<Guid>(c, "TenantId") == tenantId &&{sep}'
    f'                           !c.IsDeleted)',
    f'                .Where(c => c.DeviceId == deviceId && !c.IsDeleted)'
))

# GetAllActiveAsync: remove EF.Property TenantId (only filter left is !IsDeleted)
replacements.append((
    f'                .Where(c => EF.Property<Guid>(c, "TenantId") == tenantId &&{sep}'
    f'                           !c.IsDeleted)',
    f'                .Where(c => !c.IsDeleted)'
))

# ExistsByDeviceIdAsync
replacements.append((
    f'                .AnyAsync(c => c.DeviceId == deviceId &&{sep}'
    f'                             EF.Property<Guid>(c, "TenantId") == tenantId &&{sep}'
    f'                             !c.IsDeleted);',
    f'                .AnyAsync(c => c.DeviceId == deviceId && !c.IsDeleted);'
))

# GetWithOrdersAsync
replacements.append((
    f'                .Where(c => c.Id == id &&{sep}'
    f'                           EF.Property<Guid>(c, "TenantId") == tenantId &&{sep}'
    f'                           !c.IsDeleted)',
    f'                .Where(c => c.Id == id && !c.IsDeleted)'
))

# GetByPhoneAsync
replacements.append((
    f'                .Where(c => c.PhoneNumber == phoneNumber &&{sep}'
    f'                           EF.Property<Guid>(c, "TenantId") == tenantId &&{sep}'
    f'                           !c.IsDeleted)',
    f'                .Where(c => c.PhoneNumber == phoneNumber && !c.IsDeleted)'
))

results = []
for old, new in replacements:
    count = content.count(old)
    results.append(f'Pattern found: {count}, preview: {repr(old[:50])}')
    content = content.replace(old, new)

# Also remove now-unused tenantId local variable declarations
# They have pattern: Guid tenantId = _currentTenantId;\n
old_tenantid_var = f'            Guid tenantId = _currentTenantId;{sep}'
remaining = content.count(old_tenantid_var)
results.append(f'tenantId var declarations to remove: {remaining}')
content = content.replace(old_tenantid_var, '')

# Remove Microsoft.EntityFrameworkCore using if EF.Property is no longer used
if 'EF.Property' not in content and 'EF.' not in content:
    content = content.replace('using Microsoft.EntityFrameworkCore;\n', '')
    results.append('Removed EF using (no longer needed)')
else:
    results.append('Kept EF using (still referenced)')

with open(filepath, 'wb') as f:
    f.write(content.encode('utf-8'))

with open('C:\\VibeCoding\\Gemini_Windsurf\\scripts\\cust_v3_result.txt', 'w') as log:
    log.write('\n'.join(results) + '\n')
    log.write('Done.\n')
    remaining_ef = content.count('EF.Property')
    log.write(f'Remaining EF.Property occurrences: {remaining_ef}\n')
