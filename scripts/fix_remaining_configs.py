"""Fix remaining 3 config files with TenantId converters"""
import re, os

def fix_file(fpath, patterns):
    with open(fpath, 'rb') as f:
        raw = f.read()
    is_bom = raw.startswith(b'\xef\xbb\xbf')
    content = raw.decode('utf-8-sig')
    orig = content
    for old, new in patterns:
        content = content.replace(old, new)
    if content != orig:
        with open(fpath, 'wb') as f:
            enc = 'utf-8-sig' if is_bom else 'utf-8'
            f.write(content.encode(enc))
        return True
    return False

base = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations'

# AccountingEntryConfiguration.cs - CRLF, has SetValueComparer
p1 = os.path.join(base, 'AccountingEntryConfiguration.cs')
with open(p1, 'rb') as f: c1 = f.read().decode('utf-8-sig')
# Find the block
m = re.search(
    r'            // Use inline TenantId conversion for SQLite compatibility\r\n'
    r'            builder\.Property\(e => e\.TenantId\)\r\n'
    r'                \.IsRequired\(\)\r\n'
    r'                \.HasConversion\(id => id\.Value, value => new TenantId\(value\)\)\r\n'
    r'                \.Metadata\.SetValueComparer\(new Microsoft\.EntityFrameworkCore\.ChangeTracking\.ValueComparer<TenantId>\(',
    c1
)
if m:
    # Find the end of this block (closing ));)
    start = m.start()
    end = c1.find('));', start) + len('));')
    # Also skip trailing newline
    if c1[end:end+2] == '\r\n':
        end += 2
    block = c1[start:end]
    print(f'AccountingEntry block to remove ({len(block)} chars):\n{repr(block[:100])}')
    c1 = c1[:start] + c1[end:]
    with open(p1, 'wb') as f: f.write(c1.encode('utf-8-sig'))
    print('AccountingEntryConfiguration.cs fixed')
else:
    print('AccountingEntryConfiguration.cs: block not found with CRLF regex')
    # Try simpler approach
    idx = c1.find('HasConversion(id => id.Value, value => new TenantId(value))')
    print(f'  HasConversion idx: {idx}')
    if idx >= 0:
        print(f'  Context: {repr(c1[idx-150:idx+200])}')

# AuditLogConfiguration.cs - CRLF, has SetValueComparer  
p2 = os.path.join(base, 'AuditLogConfiguration.cs')
with open(p2, 'rb') as f: c2 = f.read().decode('utf-8-sig')
m2 = re.search(
    r'            // TenantId with converter for SQLite compatibility\r\n'
    r'            builder\.Property\(e => e\.TenantId\)\r\n'
    r'                \.IsRequired\(\)\r\n'
    r'                \.HasConversion\(id => id\.Value, value => new TenantId\(value\)\)\r\n'
    r'                \.Metadata\.SetValueComparer\(new Microsoft\.EntityFrameworkCore\.ChangeTracking\.ValueComparer<TenantId>\(',
    c2
)
if m2:
    start2 = m2.start()
    end2 = c2.find('));', start2) + len('));')
    if c2[end2:end2+2] == '\r\n':
        end2 += 2
    block2 = c2[start2:end2]
    print(f'\nAuditLog block to remove ({len(block2)} chars)')
    c2 = c2[:start2] + c2[end2:]
    with open(p2, 'wb') as f: f.write(c2.encode('utf-8-sig'))
    print('AuditLogConfiguration.cs fixed')
else:
    print('AuditLogConfiguration.cs: block not found')

# TenantConfiguration.cs - LF, uses .Id not .TenantId
p3 = os.path.join(base, 'TenantConfiguration.cs')
with open(p3, 'rb') as f: c3 = f.read().decode('utf-8-sig')
# The Id property here is TenantId type - this is a special case (PK mapping)
# We should keep this one since it maps Id (TenantId) -> Guid for PK
# ConfigureConventions applies to PROPERTIES named TenantId, not Id
# So leave TenantConfiguration.Id mapping as-is
print(f'\nTenantConfiguration.cs: Id property mapping - keeping as-is (special PK case)')
print(f'  (ConfigureConventions applies to property named TenantId, not Id)')
