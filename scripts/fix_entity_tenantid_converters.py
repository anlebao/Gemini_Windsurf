"""
Add HasConversion for TenantId to OrderConfiguration.cs and InvoiceItemConfiguration.cs.
These entities inherit BaseEntity (IMustHaveTenant) and get global query filters,
but their configurations are missing TenantId property mapping.
"""

# ─── OrderConfiguration.cs ───────────────────────────────────────────────────
order_path = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\OrderConfiguration.cs'

with open(order_path, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

# Replace the comment-only line with actual converter
old = '            // TenantId converter is configured globally in VanAnDbContext.ConfigureConventions\r\n        }'
new = (
    '            // TenantId value object converter\r\n'
    '            _ = builder.Property(o => o.TenantId)\r\n'
    '                .IsRequired()\r\n'
    '                .HasConversion(id => id.Value, value => new TenantId(value));\r\n'
    '        }'
)

count = content.count(old)
print(f'OrderConfiguration — old pattern found: {count}')
if count == 0:
    # Try LF only
    old_lf = old.replace('\r\n', '\n')
    count_lf = content.count(old_lf)
    print(f'  -> LF variant found: {count_lf}')
    if count_lf > 0:
        content = content.replace(old_lf, new.replace('\r\n', '\n'))

content = content.replace(old, new)

with open(order_path, 'wb') as f:
    f.write(content.encode('utf-8'))
print('OrderConfiguration written.')


# ─── InvoiceItemConfiguration.cs ─────────────────────────────────────────────
invoice_path = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\InvoiceItemConfiguration.cs'

with open(invoice_path, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

# Add TenantId property before the HasOne navigation at end of Configure method
# InvoiceItem does NOT have TenantId in its Configuration at all — need to add before closing brace
# Find the HasOne block and insert before it

# The file ends with a navigation property then closing braces
# We'll add after HasOne...Cascade block, before the last closing braces
old_end = (
    '            _ = builder.HasOne(e => e.Invoice)\r\n'
    '                  .WithMany(i => i.Items)\r\n'
    '                  .HasForeignKey(e => e.InvoiceId)\r\n'
    '                  .HasPrincipalKey(i => i.InvoiceId)\r\n'
    '                  .OnDelete(DeleteBehavior.Cascade);\r\n'
    '        }\r\n'
    '    }\r\n'
    '}'
)
new_end = (
    '            _ = builder.HasOne(e => e.Invoice)\r\n'
    '                  .WithMany(i => i.Items)\r\n'
    '                  .HasForeignKey(e => e.InvoiceId)\r\n'
    '                  .HasPrincipalKey(i => i.InvoiceId)\r\n'
    '                  .OnDelete(DeleteBehavior.Cascade);\r\n'
    '\r\n'
    '            // TenantId value object converter (inherited from BaseEntity)\r\n'
    '            _ = builder.Property(e => e.TenantId)\r\n'
    '                .IsRequired()\r\n'
    '                .HasConversion(id => id.Value, value => new TenantId(value));\r\n'
    '        }\r\n'
    '    }\r\n'
    '}'
)

# Also need to add using for TenantId — check if it's already there
print(f'InvoiceItemConfiguration — has VanAn.Shared.Domain: {"VanAn.Shared.Domain" in content}')

count = content.count(old_end)
print(f'InvoiceItemConfiguration — old end pattern found: {count}')
content = content.replace(old_end, new_end)

# Make sure TenantId namespace is imported
if 'VanAn.Shared.Domain' not in content:
    content = content.replace(
        'using Microsoft.EntityFrameworkCore;',
        'using Microsoft.EntityFrameworkCore;\r\nusing VanAn.Shared.Domain;'
    )
    print('  -> Added VanAn.Shared.Domain using')

with open(invoice_path, 'wb') as f:
    f.write(content.encode('utf-8'))
print('InvoiceItemConfiguration written.')

print('\nDone.')
