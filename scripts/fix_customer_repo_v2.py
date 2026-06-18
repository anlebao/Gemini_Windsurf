"""
Fix CustomerRepository.cs:
Replace c.TenantId.Value == tenantId with EF.Property<Guid>(c, "TenantId") == tenantId
to avoid IConvertible error on SQLite when comparing Guid to TEXT column.

Also check KitchenService.cs navigation property issue.
"""

import os

# ─── CustomerRepository.cs ───────────────────────────────────────────────────
customer_path = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Repositories\CustomerRepository.cs'

with open(customer_path, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

# Add EF using if needed
if 'using Microsoft.EntityFrameworkCore;' not in content:
    content = 'using Microsoft.EntityFrameworkCore;\n' + content

old = 'c.TenantId.Value == tenantId'
new = 'EF.Property<Guid>(c, "TenantId") == tenantId'

count = content.count(old)
content = content.replace(old, new)

with open(customer_path, 'wb') as f:
    f.write(content.encode('utf-8'))

result_lines = [
    f'CustomerRepository: replaced {count} occurrences of c.TenantId.Value == tenantId',
    f'  Remaining: {content.count(old)}',
]

# ─── KitchenService.cs ───────────────────────────────────────────────────────
# oi.Order.TenantId.Value == shopId — navigation property, cannot use EF.Property directly
# SQLite should handle this IF Order.TenantId has HasConversion — now it does.
# BUT the .Value syntax may still fail on SQLite.
# Alternative: Join Orders table with where clause
# For now, keep .Value and check if it works after OrderConfiguration fix.
# If still fails, we'll need to restructure the query.
kitchen_path = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\KitchenService.cs'
with open(kitchen_path, 'rb') as f:
    k = f.read().decode('utf-8-sig')

nav_count = k.count('.TenantId.Value == shopId')
result_lines.append(f'KitchenService: {nav_count} occurrences of .TenantId.Value == shopId (nav property, keeping as-is)')

with open('C:\\VibeCoding\\Gemini_Windsurf\\scripts\\fix_customer_v2_result.txt', 'w') as log:
    log.write('\n'.join(result_lines) + '\n')
    log.write('Done.\n')
