"""
Revert TenantId converters from string provider back to Guid provider.
EF Core SQLite has built-in Guid<->TEXT (UUID) handling that bypasses IConvertible.
The original IConvertible error was caused by EF.Property<Guid> + Expression.Constant(Guid)
in the global query filter - now fixed with property access pattern.

Changes:
1. All configuration files: HasConversion(new ValueConverter<TenantId, string>(...)) 
   -> HasConversion(id => id.Value, value => new TenantId(value))
2. TenantIdConverter: ValueConverter<TenantId, string> -> ValueConverter<TenantId, Guid>
3. ConfigureConventions: HaveConversion<TenantIdConverter>() (keep)
4. Global filter: CurrentTenantIdValue (TenantId) for parameter (keep - correct)
5. TenantIdConverterTests: update to expect Guid
"""

import os, re

configs_dir = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations'
results = []

OLD_NEW_VALUECONVERTER = 'HasConversion(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<TenantId, string>(id => id.Value.ToString(), str => new TenantId(Guid.Parse(str))))'
NEW_GUID_CONVERTER = 'HasConversion(id => id.Value, value => new TenantId(value))'

for fname in sorted(os.listdir(configs_dir)):
    if not fname.endswith('.cs'): continue
    fpath = os.path.join(configs_dir, fname)
    with open(fpath, 'rb') as f:
        raw = f.read()
    content = raw.decode('utf-8-sig')
    orig = content

    count = content.count(OLD_NEW_VALUECONVERTER)
    content = content.replace(OLD_NEW_VALUECONVERTER, NEW_GUID_CONVERTER)

    if content != orig:
        with open(fpath, 'wb') as f:
            f.write(content.encode('utf-8'))
        results.append(f'Reverted ({count}x): {fname}')

print('\n'.join(results))
print(f'\nTotal files: {len(results)}')
