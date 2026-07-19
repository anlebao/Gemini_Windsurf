import json, sys
runs = json.load(sys.stdin)
for r in runs:
    print(f'{r["name"]}: {r["status"]}/{r["conclusion"]}')
