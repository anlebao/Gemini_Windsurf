filepath = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\KitchenService.cs'

with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

old = '.Where(o => o.TenantId == new TenantId(shopId) && o.OrderDate >= from)'
new = '.Where(o => o.TenantId.Value == shopId && o.OrderDate >= from)'

count = content.count(old)
print(f'Found: {count}')
content = content.replace(old, new)

with open(filepath, 'wb') as f:
    f.write(content.encode('utf-8'))

remaining = content.count('new TenantId(shopId)')
print(f'Remaining new TenantId(shopId): {remaining}')
print('Done.')
