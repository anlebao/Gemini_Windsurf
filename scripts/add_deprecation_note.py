src = '.devin/rules/.windsurfrules'
with open(src, 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

note = (
    '# =============================================\n'
    '# DEPRECATED LOCATION - NOT AUTO-LOADED\n'
    '# Devin/Windsurf does NOT auto-load rules from .devin/rules/.\n'
    '# The ACTIVE always-on copies now live at:\n'
    '#   .windsurf/rules/governance.md       (governance, trigger: always_on)\n'
    '#   .windsurf/rules/session-context.md  (session context loading, always_on)\n'
    '# Edit those files for any rule changes. This file is reference-only.\n'
    '# =============================================\n\n'
)

if 'DEPRECATED LOCATION' not in content:
    content = note + content
    with open(src, 'w', encoding='utf-8') as f:
        f.write(content)
    print('deprecation note added')
else:
    print('note already present')
