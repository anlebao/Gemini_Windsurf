invoice_path = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\InvoiceItemConfiguration.cs'

with open(invoice_path, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

# Detect line ending
has_crlf = b'\r\n' in raw
sep = '\r\n' if has_crlf else '\n'

# Pattern to find — the closing of HasOne block + method close + class close + namespace close
old_end = (
    '            _ = builder.HasOne(e => e.Invoice)\n'
    '                  .WithMany(i => i.Items)\n'
    '                  .HasForeignKey(e => e.InvoiceId)\n'
    '                  .HasPrincipalKey(i => i.InvoiceId)\n'
    '                  .OnDelete(DeleteBehavior.Cascade);\n'
    '        }\n'
    '    }\n'
    '}'
)
new_end = (
    '            _ = builder.HasOne(e => e.Invoice)\n'
    '                  .WithMany(i => i.Items)\n'
    '                  .HasForeignKey(e => e.InvoiceId)\n'
    '                  .HasPrincipalKey(i => i.InvoiceId)\n'
    '                  .OnDelete(DeleteBehavior.Cascade);\n'
    '\n'
    '            // TenantId value object converter (inherited from BaseEntity)\n'
    '            _ = builder.Property(e => e.TenantId)\n'
    '                .IsRequired()\n'
    '                .HasConversion(id => id.Value, value => new TenantId(value));\n'
    '        }\n'
    '    }\n'
    '}'
)

found = old_end in content
with open('C:\\VibeCoding\\Gemini_Windsurf\\scripts\\invoice_result.txt', 'w') as log:
    log.write(f'has_crlf: {has_crlf}\n')
    log.write(f'old_end found: {found}\n')
    if found:
        content = content.replace(old_end, new_end)
        log.write('replaced.\n')
    else:
        log.write('NOT found\n')
        log.write(f'file tail: {repr(content[-200:])}\n')

with open(invoice_path, 'wb') as f:
    f.write(content.encode('utf-8'))
