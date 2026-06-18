"""
Remove per-property HasConversion for TenantId from all configuration files.
The TenantIdConverter (registered via ConfigureConventions) will handle all TenantId properties globally.
Per-property HasConversion OVERRIDES the convention converter, preventing the override from working.
"""
import os, re

configs_dir = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations'
results = []

# The various TenantId HasConversion patterns we need to remove:
PATTERNS = [
    # Pattern: full block with comment
    (
        r'            // TenantId .*?\r\n'
        r'            _ = builder\.Property\(e => e\.TenantId\)\r\n'
        r'                \.IsRequired\(\)\r\n'
        r'                \.HasConversion\(id => id\.Value, value => new TenantId\(value\)\);',
        ''
    ),
    (
        r'            // Use inline TenantId conversion for SQLite compatibility\r\n'
        r'            builder\.Property\(e => e\.TenantId\)\r\n'
        r'                \.IsRequired\(\)\r\n'
        r'                \.HasConversion\(id => id\.Value, value => new TenantId\(value\)\);',
        ''
    ),
    # Without comment lines
    (
        r'            _ = builder\.Property\(e => e\.TenantId\)\r\n'
        r'                \.IsRequired\(\)\r\n'
        r'                \.HasConversion\(id => id\.Value, value => new TenantId\(value\)\);',
        ''
    ),
    # Inline (single line with _ =)
    (
        r'            _ = builder\.Property\(e => e\.TenantId\)\.IsRequired\(\)\.HasConversion\(id => id\.Value, value => new TenantId\(value\)\);',
        ''
    ),
    # With SetValueComparer (multiline from older configs)
    (
        r'            builder\.Property\(e => e\.TenantId\)\r\n'
        r'                \.IsRequired\(\)\r\n'
        r'                \.HasConversion\(\r\n'
        r'                    id => id\.Value\.ToString\(\),\r\n'
        r'                    value => new TenantId\(Guid\.Parse\(value\)\)\)',
        # keep IsRequired but remove HasConversion
        '            builder.Property(e => e.TenantId)\r\n                .IsRequired()'
    ),
]

# Also simpler patterns:
SIMPLE_PATTERNS = [
    (
        '                .HasConversion(id => id.Value, value => new TenantId(value));',
        ';'
    ),
]

for fname in sorted(os.listdir(configs_dir)):
    if not fname.endswith('.cs'): continue
    fpath = os.path.join(configs_dir, fname)
    with open(fpath, 'rb') as f:
        raw = f.read()
    content = raw.decode('utf-8-sig')
    orig = content

    for pattern, repl in PATTERNS:
        content = re.sub(pattern, repl, content)

    if content != orig:
        with open(fpath, 'wb') as f:
            f.write(content.encode('utf-8'))
        results.append(f'Cleaned: {fname}')

print('\n'.join(results) if results else 'No changes (patterns not found)')

# Now check what TenantId HasConversion remains
remaining = []
for fname in sorted(os.listdir(configs_dir)):
    if not fname.endswith('.cs'): continue
    fpath = os.path.join(configs_dir, fname)
    with open(fpath, 'rb') as f:
        c = f.read().decode('utf-8-sig')
    if 'HasConversion(id => id.Value, value => new TenantId' in c:
        remaining.append(fname)
        
print(f'\nRemaining HasConversion for TenantId: {remaining}')
