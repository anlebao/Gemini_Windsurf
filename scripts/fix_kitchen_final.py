p = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\KitchenService.cs'
with open(p, 'rb') as f:
    c = f.read().decode('utf-8-sig')

old = (
    '            // \U0001f6e1\ufe0f STEP 1: SQL Projection - Server-side filtering & flat projection\r\n'
    '            string shopIdStr = shopId.ToString();\r\n'
    '            var flatItems = await _context.OrderItems\r\n'
    '                .Where(oi => EF.Property<string>(oi.Order, "TenantId") == shopIdStr &&\r\n'
    '                            (oi.KitchenStatus == KitchenStatus.Pending || oi.KitchenStatus == KitchenStatus.Preparing))'
)

new = (
    '            // \U0001f6e1\ufe0f STEP 1: SQL Projection - Server-side filtering & flat projection\r\n'
    '            // Use TenantId strongly-typed comparison so EF Core Sanitize<TenantId>(TenantId) passes.\r\n'
    '            TenantId tenantId = new(shopId);\r\n'
    '            var flatItems = await _context.OrderItems\r\n'
    '                .Where(oi => oi.Order.TenantId == tenantId &&\r\n'
    '                            (oi.KitchenStatus == KitchenStatus.Pending || oi.KitchenStatus == KitchenStatus.Preparing))'
)

count = c.count(old)
print(f'Found: {count}')
if count:
    c = c.replace(old, new)
    with open(p, 'wb') as f:
        f.write(c.encode('utf-8'))
    print('Written')
else:
    idx = c.find('shopIdStr')
    print('Context:', repr(c[max(0,idx-100):idx+250]))
