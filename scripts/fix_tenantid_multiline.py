import os, re

configs = [
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\CustomerConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\JournalEntryConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\JournalTemplateConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\AuditLogConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\UserConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\LeadConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\SystemMetricsConfiguration.cs',
    r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations\OutboxMessageConfiguration.cs',
]

# Pattern: multiline HasConversion with Guid
# .HasConversion(
#     id => id.Value,
#     value => new TenantId(value));
# OR .HasConversion(id => id.Value, value => new TenantId(value));

# Also match TenantIdConverter which converts to Guid
# new TenantIdConverter()  — also needs fixing

patterns_replacements = [
    # Multiline lambda: id => id.Value \n value => new TenantId(value)
    (
        re.compile(r'\.HasConversion\(\s*\n\s*id => id\.Value,\s*\n\s*value => new TenantId\(value\)\)', re.MULTILINE),
        '.HasConversion(\n                    id => id.Value.ToString(),\n                    value => new TenantId(Guid.Parse(value)))'
    ),
    # Inline: already fixed in previous script, but just in case
    (
        re.compile(r'HasConversion\(id => id\.Value, value => new TenantId\(value\)\)'),
        'HasConversion(id => id.Value.ToString(), value => new TenantId(Guid.Parse(value)))'
    ),
]

log = open(r'C:\VibeCoding\Gemini_Windsurf\scripts\multiline_result.txt', 'w')

for path in configs:
    name = os.path.basename(path)
    if not os.path.exists(path):
        log.write('SKIP: ' + name + '\n')
        continue
    with open(path, 'rb') as f:
        raw = f.read()
    content = raw.decode('utf-8-sig')
    total_count = 0
    for pattern, repl in patterns_replacements:
        matches = pattern.findall(content)
        if matches:
            content = pattern.sub(repl, content)
            total_count += len(matches)
    with open(path, 'wb') as f:
        f.write(content.encode('utf-8'))
    log.write(f'Fixed {total_count}x: {name}\n')

log.close()

# Now check all config files for TenantIdConverter usage (converts to Guid — same issue)
config_dir = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Configurations'
log2 = open(r'C:\VibeCoding\Gemini_Windsurf\scripts\tenantid_converter_usage.txt', 'w')
for fname in os.listdir(config_dir):
    fpath = os.path.join(config_dir, fname)
    if not fpath.endswith('.cs'):
        continue
    with open(fpath, 'rb') as f:
        c = f.read().decode('utf-8-sig')
    if 'TenantIdConverter' in c or 'id => id.Value' in c:
        log2.write(f'{fname}: TenantIdConverter={("TenantIdConverter" in c)}, id.Value={"id => id.Value" in c}\n')
        # Extract the relevant lines
        for i, line in enumerate(c.split('\n'), 1):
            if 'TenantId' in line and ('Conversion' in line or 'Converter' in line or 'id.Value' in line):
                log2.write(f'  L{i}: {line.rstrip()}\n')
log2.close()
