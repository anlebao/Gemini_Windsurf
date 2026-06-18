"""
Fix KitchenService.cs and CustomerRepository.cs:
Replace `new TenantId(x)` in LINQ predicates with `.Value == x` (Guid primitive)
so SQLite can translate the expressions.
"""

def fix_file(filepath, replacements):
    with open(filepath, 'rb') as f:
        raw = f.read()
    content = raw.decode('utf-8-sig')

    for old, new in replacements:
        count = content.count(old)
        print(f'  [{filepath.split(chr(92))[-1]}] "{old[:60]}..." -> found {count} time(s)')
        if count == 0:
            # Try with CRLF variants
            old_crlf = old.replace('\n', '\r\n')
            count2 = content.count(old_crlf)
            if count2 > 0:
                content = content.replace(old_crlf, new.replace('\n', '\r\n'))
                print(f'    -> replaced {count2} CRLF occurrence(s)')
            else:
                print(f'    -> WARNING: not found at all, skipping')
        else:
            content = content.replace(old, new)
            print(f'    -> replaced {count} occurrence(s)')

    with open(filepath, 'wb') as f:
        f.write(content.encode('utf-8'))
    print(f'  Written: {filepath}')


# ─── KitchenService.cs ───────────────────────────────────────────────────────
kitchen_path = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\KitchenService.cs'

kitchen_replacements = [
    # Line 23: GetGroupedKitchenItemsAsync
    (
        '.Where(oi => oi.Order.TenantId == new TenantId(shopId) &&',
        '.Where(oi => oi.Order.TenantId.Value == shopId &&',
    ),
    # Line 155: GetPendingItemsCountAsync
    (
        '.CountAsync(oi => oi.Order.TenantId == new TenantId(shopId) && oi.KitchenStatus == KitchenStatus.Pending);',
        '.CountAsync(oi => oi.Order.TenantId.Value == shopId && oi.KitchenStatus == KitchenStatus.Pending);',
    ),
    # Line 162: GetAveragePreparationTimeAsync
    (
        '.Where(oi => oi.Order.TenantId == new TenantId(shopId) &&',
        '.Where(oi => oi.Order.TenantId.Value == shopId &&',
    ),
]

print('=== Fixing KitchenService.cs ===')
fix_file(kitchen_path, kitchen_replacements)

# Verify
with open(kitchen_path, 'rb') as f:
    k = f.read().decode('utf-8-sig')
remaining = k.count('new TenantId(shopId)')
print(f'  Remaining "new TenantId(shopId)": {remaining}')


# ─── CustomerRepository.cs ───────────────────────────────────────────────────
customer_path = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Repositories\CustomerRepository.cs'

# All 6 occurrences use the same pattern: c.TenantId == new TenantId(tenantId)
customer_replacements = [
    (
        'c.TenantId == new TenantId(tenantId)',
        'c.TenantId.Value == tenantId',
    ),
]

print('\n=== Fixing CustomerRepository.cs ===')
fix_file(customer_path, customer_replacements)

with open(customer_path, 'rb') as f:
    c = f.read().decode('utf-8-sig')
remaining = c.count('new TenantId(tenantId)')
print(f'  Remaining "new TenantId(tenantId)": {remaining}')

print('\nDone.')
