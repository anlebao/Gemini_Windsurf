"""
Fix SystemMetricsRepository.cs:
Replace .Select(o => o.TenantId).Distinct() with .Select(o => o.TenantId.Value).Distinct()
so SQLite can perform Distinct() on Guid primitives instead of Strongly-Typed ID objects.
"""

filepath = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Repositories\SystemMetricsRepository.cs'

with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

print('Original content around TenantId.Distinct:')
idx = content.find('.Select(o => o.TenantId)')
print(repr(content[idx-10:idx+50]))

old = '.Select(o => o.TenantId)'
new = '.Select(o => o.TenantId.Value)'

count = content.count(old)
print(f'Found: {count}')
content = content.replace(old, new)

with open(filepath, 'wb') as f:
    f.write(content.encode('utf-8'))

remaining = content.count('.Select(o => o.TenantId)')
print(f'Remaining ".Select(o => o.TenantId)": {remaining}')
print('Done.')
