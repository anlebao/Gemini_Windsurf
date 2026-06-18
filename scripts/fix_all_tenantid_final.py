"""
Final fix: scan ALL *.cs in Configurations/ and replace ALL patterns:
1. HasConversion(id => id.Value, value => new TenantId(value)) [inline & multiline]
2. HasConversion<TenantIdConverter>() — TenantIdConverter converts to Guid, need string
3. HasConversion(new TenantIdConverter()) — same

Also fixes TenantIdConverter.cs to use string as provider type.

NOTE: TenantConfiguration.cs and TenantIdConfiguration.cs also checked.
"""
import os, re

config_dir = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations'

log = open(r'C:\VibeCoding\Gemini_Windsurf\scripts\final_fix_result.txt', 'w')

for fname in sorted(os.listdir(config_dir)):
    if not fname.endswith('.cs'):
        continue
    fpath = os.path.join(config_dir, fname)
    with open(fpath, 'rb') as f:
        raw = f.read()
    content = raw.decode('utf-8-sig')
    original = content
    
    changes = 0
    
    # Pattern 1: multiline lambda
    # .HasConversion(\n..id => id.Value,\n..value => new TenantId(value))
    p1 = re.compile(
        r'\.HasConversion\(\s*\r?\n\s*id => id\.Value,\s*\r?\n\s*value => new TenantId\(value\)\)',
        re.MULTILINE
    )
    m1 = len(p1.findall(content))
    content = p1.sub(
        '.HasConversion(\n                    id => id.Value.ToString(),\n                    value => new TenantId(Guid.Parse(value)))',
        content
    )
    changes += m1
    
    # Pattern 2: inline lambda (any spacing)
    p2 = re.compile(r'HasConversion\(id => id\.Value, value => new TenantId\(value\)\)')
    m2 = len(p2.findall(content))
    content = p2.sub(
        'HasConversion(id => id.Value.ToString(), value => new TenantId(Guid.Parse(value)))',
        content
    )
    changes += m2
    
    # Pattern 3: HasConversion<TenantIdConverter>() → inline lambda string
    p3 = re.compile(r'HasConversion<TenantIdConverter>\(\)')
    m3 = len(p3.findall(content))
    content = p3.sub(
        'HasConversion(id => id.Value.ToString(), value => new TenantId(Guid.Parse(value)))',
        content
    )
    changes += m3
    
    # Pattern 4: HasConversion(new TenantIdConverter()) → inline lambda string
    p4 = re.compile(r'HasConversion\(new TenantIdConverter\(\)\)')
    m4 = len(p4.findall(content))
    content = p4.sub(
        'HasConversion(id => id.Value.ToString(), value => new TenantId(Guid.Parse(value)))',
        content
    )
    changes += m4
    
    if content != original:
        with open(fpath, 'wb') as f:
            f.write(content.encode('utf-8'))
        log.write(f'Fixed ({m1}ml+{m2}il+{m3}cls+{m4}new={changes}): {fname}\n')
    else:
        # Report if file still has old patterns
        remaining = (
            'id => id.Value,' in content or 
            'id => id.Value)' in content or
            'TenantIdConverter' in content
        )
        if remaining:
            log.write(f'WARN still has patterns: {fname}\n')
            for i, line in enumerate(content.split('\n'), 1):
                if 'TenantId' in line and ('Conversion' in line or 'id.Value' in line):
                    log.write(f'  L{i}: {line.rstrip()}\n')

log.close()

# Also fix TenantIdConverter.cs itself to use string provider type
converter_path = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\ValueConverters\TenantIdConverter.cs'
if os.path.exists(converter_path):
    with open(converter_path, 'rb') as f:
        raw = f.read()
    content = raw.decode('utf-8-sig')
    old = 'public sealed class TenantIdConverter : ValueConverter<TenantId, Guid>'
    new = 'public sealed class TenantIdConverter : ValueConverter<TenantId, string>'
    if old in content:
        content = content.replace(old, new)
        # Fix body
        old2 = 'convertToProviderExpression: tenantId => tenantId.Value,'
        new2 = 'convertToProviderExpression: tenantId => tenantId.Value.ToString(),'
        content = content.replace(old2, new2)
        old3 = 'convertFromProviderExpression: guid => new TenantId(guid))'
        new3 = 'convertFromProviderExpression: str => new TenantId(Guid.Parse(str)))'
        content = content.replace(old3, new3)
        with open(converter_path, 'wb') as f:
            f.write(content.encode('utf-8'))
        with open(r'C:\VibeCoding\Gemini_Windsurf\scripts\final_fix_result.txt', 'a') as log:
            log.write('Fixed TenantIdConverter.cs: Guid→string provider type\n')
