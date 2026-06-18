"""
Fix SystemMetricsRepository.cs:
.Select(o => o.TenantId.Value) causes "Object must implement IConvertible" on SQLite
because Guid is not IConvertible and SQLite stores TenantId as TEXT.

Replace with EF.Property<Guid>(o, "TenantId") which SQLite can translate correctly.
Also need to add EF using if not present.
"""

filepath = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Repositories\SystemMetricsRepository.cs'

with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

with open('C:\\VibeCoding\\Gemini_Windsurf\\scripts\\metrics_v2_result.txt', 'w') as log:
    log.write(f'Original has_crlf: {chr(13)+chr(10) in content}\n')
    log.write(f'Has TenantId.Value: {".Select(o => o.TenantId.Value)" in content}\n')
    log.write(f'Has Microsoft.EntityFrameworkCore using: {"Microsoft.EntityFrameworkCore" in content}\n')

# Add EF using if needed
if 'using Microsoft.EntityFrameworkCore;' not in content:
    content = 'using Microsoft.EntityFrameworkCore;\n' + content

# Replace both occurrences of .Select(o => o.TenantId.Value)
old = '.Select(o => o.TenantId.Value)'
new = '.Select(o => EF.Property<Guid>(o, "TenantId"))'

count = content.count(old)

with open('C:\\VibeCoding\\Gemini_Windsurf\\scripts\\metrics_v2_result.txt', 'a') as log:
    log.write(f'Count of pattern to replace: {count}\n')

content = content.replace(old, new)

with open(filepath, 'wb') as f:
    f.write(content.encode('utf-8'))

remaining = content.count('.Select(o => o.TenantId.Value)')
with open('C:\\VibeCoding\\Gemini_Windsurf\\scripts\\metrics_v2_result.txt', 'a') as log:
    log.write(f'Remaining after replace: {remaining}\n')
    log.write('Done.\n')
