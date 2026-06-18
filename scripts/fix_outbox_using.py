filepath = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\OutboxMessageConfiguration.cs'
with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

# Add VanAn.Shared.Domain using after ValueConverters using
old = 'using VanAn.CoreHub.Infrastructure.ValueConverters;\r\n'
new = 'using VanAn.CoreHub.Infrastructure.ValueConverters;\r\nusing VanAn.Shared.Domain;\r\n'
count = content.count(old)
content = content.replace(old, new)
with open(filepath, 'wb') as f:
    f.write(content.encode('utf-8'))
print(f'Added using: {count}x')
