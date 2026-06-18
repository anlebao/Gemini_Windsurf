src = '.devin/rules/.windsurfrules'
try:
    with open(src, 'r', encoding='utf-8') as f:
        content = f.read()
except UnicodeDecodeError:
    with open(src, 'r', encoding='utf-8', errors='ignore') as f:
        content = f.read()

# Extract governance from '## CORE PRINCIPLES' onward (skip the AUTO-CONTEXT header)
idx = content.find('## CORE PRINCIPLES')
gov = content[idx:].rstrip() + '\n'

frontmatter = (
    '---\n'
    'description: "VanAn Ecosystem core governance: Domain integrity, workflow modes, UI Platform, hard stops"\n'
    'trigger: always_on\n'
    '---\n\n'
    '# VANAN ECOSYSTEM GOVERNANCE (v7.0)\n\n'
)

with open('.windsurf/rules/governance.md', 'w', encoding='utf-8') as f:
    f.write(frontmatter + gov)

print('governance.md created, governance chars:', len(gov))
