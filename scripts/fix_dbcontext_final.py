"""
Full rewrite of VanAnDbContext.cs to:
1. Remove orphan old code after ApplyMultiTenancyFilters
2. Add CurrentTenantIdValue property (TenantId type)
3. Keep new ApplyMultiTenancyFilters body
"""

p = r'C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\VanAnDbContext.cs'
with open(p, 'rb') as f:
    raw = f.read()
c = raw.decode('utf-8-sig')

# Step 1: Remove the orphan block (old method body floating outside method)
# It starts with "            // Get current tenant dynamically..."
# and ends with "        }"  (closing brace at col 8)
orphan_start = '\r\n            // Get current tenant dynamically from ITenantProvider\r\n'
orphan_end   = '        }\r\n\r\n        // MOVED: All ValueConverter classes'

idx_s = c.find(orphan_start)
idx_e = c.find(orphan_end)

if idx_s != -1 and idx_e != -1 and idx_s < idx_e:
    # remove from orphan_start up to (not including) orphan_end content after the brace
    remove_from = idx_s
    # remove_to = just before "        // MOVED"
    remove_to = idx_e + len('        }\r\n')
    c = c[:remove_from] + '\r\n' + c[remove_to:]
    print(f'Removed orphan block ({remove_to - remove_from} chars)')
else:
    print(f'Orphan not found: idx_s={idx_s}, idx_e={idx_e}')

# Step 2: Add CurrentTenantIdValue property after CurrentTenantIdString
old_props = (
    '        // Used by global query filter: TenantId column is stored as TEXT (UUID string)\r\n'
    '        // Both SQLite and Npgsql can compare TEXT columns with string parameters.\r\n'
    '        public string CurrentTenantIdString => CurrentTenantId.ToString();\r\n'
)
new_props = (
    '        // Used by global query filter: TenantId column is stored as TEXT (UUID string)\r\n'
    '        // Both SQLite and Npgsql can compare TEXT columns with string parameters.\r\n'
    '        public string CurrentTenantIdString => CurrentTenantId.ToString();\r\n'
    '\r\n'
    '        // TenantId value object — used by ApplyMultiTenancyFilters expression tree\r\n'
    '        // so EF Core can translate e.TenantId == CurrentTenantIdValue with the\r\n'
    '        // TenantId→string converter, emitting a properly parameterized SQL query.\r\n'
    '        public TenantId CurrentTenantIdValue => new TenantId(CurrentTenantId);\r\n'
)

count = c.count(old_props)
c = c.replace(old_props, new_props)
print(f'Property inserted: {count}x')

with open(p, 'wb') as f:
    f.write(c.encode('utf-8'))
print('Done')
