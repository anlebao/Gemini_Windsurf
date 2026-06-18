"""
Fix KitchenService.cs:
Replace .TenantId.Value == shopId (navigation property, Guid comparison)
with EF.Property<string>(o, "TenantId") == shopIdStr (string comparison).

For navigation properties (oi.Order.TenantId.Value), we restructure to use
Orders DbSet directly and filter by OrderId, or use EF.Property on the Order navigation.

Actually the simplest fix: capture shopIdStr = shopId.ToString() and use
o.TenantId.Value.ToString() ... but that won't translate either.

Best approach: use string shopIdStr and compare via the same HasConversion path.
Since TenantId has HasConversion(id => id.Value.ToString(), ...), EF Core should be able to
translate oi.Order.TenantId == new TenantId(shopId) via the converter.
Let's try: new TenantId(shopId) — EF Core applies converter: new TenantId(shopId).Value.ToString()
and compares with column TEXT. But EF Core might not evaluate constructor in lambda.

Safest: for direct Order properties (o.TenantId.Value), use EF.Property<string>(o, "TenantId")
For navigation (oi.Order.TenantId), same but on the navigation entity.

Actually EF.Property<T>(entity, prop) works only with entities in query scope.
For navigation properties, EF Core 8 supports EF.Property on joins.

BUT: The cleanest fix that works with both SQLite and Npgsql is:
  string shopIdStr = shopId.ToString();
  .Where(oi => EF.Property<string>(oi.Order, "TenantId") == shopIdStr && ...)

This should work because oi.Order is a navigation property (join) and EF.Property accesses
the underlying column.
"""
import re

filepath = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\KitchenService.cs'
with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')
original = content

# Pattern 1: method bodies with shopId param - add string capture at start of body
# and replace all .TenantId.Value == shopId with EF.Property<string>

# For GetGroupedKitchenItemsAsync: oi.Order.TenantId.Value == shopId
old1 = '            var flatItems = await _context.OrderItems\r\n                .Where(oi => oi.Order.TenantId.Value == shopId &&'
new1 = '            string shopIdStr = shopId.ToString();\r\n            var flatItems = await _context.OrderItems\r\n                .Where(oi => EF.Property<string>(oi.Order, "TenantId") == shopIdStr &&'
c1 = content.count(old1)
content = content.replace(old1, new1)

# For GetPendingItemsCountAsync: oi.Order.TenantId.Value == shopId
old2 = '                .CountAsync(oi => oi.Order.TenantId.Value == shopId && oi.KitchenStatus == KitchenStatus.Pending);'
new2 = (
    '                .Where(oi => EF.Property<string>(oi.Order, "TenantId") == shopId.ToString())\r\n'
    '                .CountAsync(oi => oi.KitchenStatus == KitchenStatus.Pending);'
)
c2 = content.count(old2)
content = content.replace(old2, new2)

# For GetAveragePreparationTimeAsync: oi.Order.TenantId.Value == shopId
old3 = '                .Where(oi => oi.Order.TenantId.Value == shopId &&\r\n                            oi.KitchenStatus == KitchenStatus.Completed &&'
new3 = '                .Where(oi => EF.Property<string>(oi.Order, "TenantId") == shopId.ToString() &&\r\n                            oi.KitchenStatus == KitchenStatus.Completed &&'
c3 = content.count(old3)
content = content.replace(old3, new3)

# For GetKitchenAnalyticsAsync: o.TenantId.Value == shopId
old4 = '                .Where(o => o.TenantId.Value == shopId && o.OrderDate >= from)'
new4 = '                .Where(o => EF.Property<string>(o, "TenantId") == shopId.ToString() && o.OrderDate >= from)'
c4 = content.count(old4)
content = content.replace(old4, new4)

with open(filepath, 'wb') as f:
    f.write(content.encode('utf-8'))

result_lines = [
    f'GetGroupedKitchenItemsAsync (flatItems): replaced {c1}x',
    f'GetPendingItemsCountAsync: replaced {c2}x',
    f'GetAveragePreparationTimeAsync: replaced {c3}x',
    f'GetKitchenAnalyticsAsync: replaced {c4}x',
    f'Remaining .TenantId.Value == shopId: {content.count(".TenantId.Value == shopId")}',
]

with open(r'C:\VibeCoding\Gemini_Windsurf\scripts\kitchen_v3_result.txt', 'w', encoding='utf-8') as log:
    log.write('\n'.join(result_lines) + '\n')
