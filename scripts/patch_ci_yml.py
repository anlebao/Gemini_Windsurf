path = r'C:\VibeCoding\Gemini_Windsurf\.github\workflows\ci.yml'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

old = """on:
  push:
    branches: [ main, develop, 'feature/**' ]
    paths-ignore:
      - '**.md'
      - 'docs/**'
      - '.devin/**'
  pull_request:
    branches: [ main, develop, 'align-consumer-phase4' ]
    paths-ignore:
      - '**.md'
      - 'docs/**'
      - '.devin/**'"""

new = """on:
  push:
    branches: [ main, develop, 'feature/**' ]
    paths-ignore:
      - '**.md'
      - 'docs/**'
      - '.devin/**'
      - '.devin/**'
  pull_request:
    branches: [ main, develop ]
    paths-ignore:
      - '**.md'
      - 'docs/**'
      - '.devin/**'
      - '.devin/**'"""

if old in content:
    content = content.replace(old, new)
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)
    print('ci.yml patched OK')
else:
    print('ERROR: pattern not found')
