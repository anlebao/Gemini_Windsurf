"""
Fix all HasConversion for TenantId to use string instead of Guid as provider type.
SQLite stores Guid as TEXT natively — converting TenantId directly to string (UUID format)
avoids the IConvertible error that occurs when SQLite tries to use Guid as a parameter type.

PostgreSQL also supports storing UUIDs as text — this is the most compatible approach.

Changes:
  HasConversion(id => id.Value, value => new TenantId(value))
  → HasConversion(id => id.Value.ToString(), value => new TenantId(Guid.Parse(value)))

Also fixes TenantIdConverter.cs to use string provider type.
"""

import os

configs = {
    'CustomerConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\CustomerConfiguration.cs',
    'OrderConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\OrderConfiguration.cs',
    'InvoiceItemConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\InvoiceItemConfiguration.cs',
    'ShopConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\ShopConfiguration.cs',
    'DemoUserConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\DemoUserConfiguration.cs',
    'IngredientConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\IngredientConfiguration.cs',
    'InventoryConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\InventoryConfiguration.cs',
    'RecipeConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\RecipeConfiguration.cs',
    'ProductConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\ProductConfiguration.cs',
    'ElectronicInvoiceConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\ElectronicInvoiceConfiguration.cs',
    'LoyaltyRewardsConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\LoyaltyRewardsConfiguration.cs',
    'SocialCampaignConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\SocialCampaignConfiguration.cs',
    'PendingInvoiceQueueConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\PendingInvoiceQueueConfiguration.cs',
}

# Pattern: lambda HasConversion for TenantId
OLD_LAMBDA = 'HasConversion(id => id.Value, value => new TenantId(value))'
NEW_LAMBDA = 'HasConversion(id => id.Value.ToString(), value => new TenantId(Guid.Parse(value)))'

results = []
for name, path in configs.items():
    if not os.path.exists(path):
        results.append(f'SKIP (not found): {name}')
        continue
    with open(path, 'rb') as f:
        raw = f.read()
    content = raw.decode('utf-8-sig')
    count = content.count(OLD_LAMBDA)
    if count > 0:
        content = content.replace(OLD_LAMBDA, NEW_LAMBDA)
        with open(path, 'wb') as f:
            f.write(content.encode('utf-8'))
        results.append(f'Fixed {count}x: {name}')
    else:
        results.append(f'No match (0): {name}')

with open('C:\\VibeCoding\\Gemini_Windsurf\\scripts\\tenantid_string_result.txt', 'w') as log:
    log.write('\n'.join(results) + '\n')

# Also check JournalEntryConfiguration and JournalTemplateConfiguration (use HasConversion<TenantIdConverter>)
# and AuditLogConfiguration (uses custom lambda)
other_configs = {
    'JournalEntryConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\JournalEntryConfiguration.cs',
    'JournalTemplateConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\JournalTemplateConfiguration.cs',
    'OutboxMessageConfiguration.cs': r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\OutboxMessageConfiguration.cs',
}
with open('C:\\VibeCoding\\Gemini_Windsurf\\scripts\\tenantid_string_result.txt', 'a') as log:
    for name, path in other_configs.items():
        if os.path.exists(path):
            with open(path, 'rb') as f:
                c = f.read().decode('utf-8-sig')
            log.write(f'{name}: HasConversion<TenantIdConverter>={chr(34)HasConversion<TenantIdConverter>{chr(34)} in c}, lambda={OLD_LAMBDA in c}\n')
