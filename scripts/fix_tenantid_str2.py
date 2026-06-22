import os

configs = [
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\CustomerConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\OrderConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\InvoiceItemConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\ShopConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\DemoUserConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\IngredientConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\InventoryConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\RecipeConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\ProductConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\ElectronicInvoiceConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\LoyaltyRewardsConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\SocialCampaignConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\PendingInvoiceQueueConfiguration.cs',
]

OLD = 'HasConversion(id => id.Value, value => new TenantId(value))'
NEW = 'HasConversion(id => id.Value.ToString(), value => new TenantId(Guid.Parse(value)))'

log = open(r'C:\VibeCoding\Gemini_Windsurf\scripts\str_result.txt', 'w')

for path in configs:
    name = os.path.basename(path)
    if not os.path.exists(path):
        log.write('SKIP: ' + name + '\n')
        continue
    with open(path, 'rb') as f:
        raw = f.read()
    content = raw.decode('utf-8-sig')
    count = content.count(OLD)
    content = content.replace(OLD, NEW)
    with open(path, 'wb') as f:
        f.write(content.encode('utf-8'))
    log.write('Fixed ' + str(count) + 'x: ' + name + '\n')

log.close()
