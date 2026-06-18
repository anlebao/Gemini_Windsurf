"""
Remove TenantId HasConversion from all configuration files.
ConfigureConventions in VanAnDbContext registers TenantIdConverter globally.
Per-property HasConversion overrides convention, so must be removed.
"""
import os, re

configs_dir = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations'
results = []

# Regex to match TenantId property with HasConversion block
# Handles both LF and CRLF, various orders of IsRequired/HasConversion
TENANT_PROPERTY_PATTERN = re.compile(
    r'[ \t]*(?:// [^\n]*\n[ \t]*)?'                         # optional comment line
    r'_ = builder\.Property\(e => e\.TenantId\)\n'          # property start (LF)
    r'(?:[ \t]+\.[^\n]+\n)*?'                               # any chained calls
    r'[ \t]+\.HasConversion\(id => id\.Value, value => new TenantId\(value\)\)'  # the HasConversion
    r'(?:\n[ \t]+\.[^\n]+)*?'                               # more chained calls after
    r'[ \t]*;[ \t]*\n',                                     # semicolon end
    re.MULTILINE
)

# Alternative pattern for CRLF
TENANT_PROPERTY_PATTERN_CRLF = re.compile(
    r'[ \t]*(?:// [^\r\n]*\r\n[ \t]*)?'
    r'_ = builder\.Property\(e => e\.TenantId\)\r\n'
    r'(?:[ \t]+\.[^\r\n]+\r\n)*?'
    r'[ \t]+\.HasConversion\(id => id\.Value, value => new TenantId\(value\)\)'
    r'(?:\r\n[ \t]+\.[^\r\n]+)*?'
    r'[ \t]*;[ \t]*\r\n',
    re.MULTILINE
)

for fname in sorted(os.listdir(configs_dir)):
    if not fname.endswith('.cs'): continue
    fpath = os.path.join(configs_dir, fname)
    with open(fpath, 'rb') as f:
        raw = f.read()
    
    # Detect encoding
    is_utf8_sig = raw.startswith(b'\xef\xbb\xbf')
    content = raw.decode('utf-8-sig')
    orig = content
    
    # Apply both patterns
    m1 = TENANT_PROPERTY_PATTERN.findall(content)
    content = TENANT_PROPERTY_PATTERN.sub('', content)
    
    m2 = TENANT_PROPERTY_PATTERN_CRLF.findall(content)
    content = TENANT_PROPERTY_PATTERN_CRLF.sub('', content)
    
    if content != orig:
        with open(fpath, 'wb') as f:
            enc = 'utf-8-sig' if is_utf8_sig else 'utf-8'
            f.write(content.encode(enc))
        results.append(f'{fname}: removed {len(m1)+len(m2)} block(s)')

print('\n'.join(results) if results else 'No regex matches')

# Check remaining
remaining = []
for fname in sorted(os.listdir(configs_dir)):
    if not fname.endswith('.cs'): continue
    with open(os.path.join(configs_dir, fname), 'rb') as f:
        c = f.read().decode('utf-8-sig')
    if 'HasConversion(id => id.Value, value => new TenantId(value))' in c:
        remaining.append(fname)
print(f'\nStill has TenantId HasConversion: {remaining}')
