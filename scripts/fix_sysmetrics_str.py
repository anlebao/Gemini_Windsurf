filepath = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Repositories\SystemMetricsRepository.cs'
with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')
old = 'EF.Property<Guid>(o, "TenantId")'
new = 'EF.Property<string>(o, "TenantId")'
count = content.count(old)
content = content.replace(old, new)
with open(filepath, 'wb') as f:
    f.write(content.encode('utf-8'))
print(f'Replaced {count}x. Remaining Guid: {content.count(old)}')
