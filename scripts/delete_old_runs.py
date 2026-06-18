import subprocess
import json

# Lấy toàn bộ run IDs cần xóa
result = subprocess.run(
    ["gh", "run", "list", "--limit", "100", "--json", "databaseId,headBranch,status"],
    capture_output=True, text=True
)
runs = json.loads(result.stdout)
keep = {r['databaseId'] for r in runs if r['headBranch'] == 'main' or r['status'] == 'in_progress'}
to_delete = [str(r['databaseId']) for r in runs if r['databaseId'] not in keep]

print(f"Deleting {len(to_delete)} runs, keeping {len(keep)}...")

deleted = 0
failed = 0
for rid in to_delete:
    res = subprocess.run(
        ["gh", "run", "delete", rid],
        capture_output=True, text=True
    )
    if res.returncode == 0:
        deleted += 1
        print(f"  deleted {rid}")
    else:
        failed += 1
        print(f"  FAILED {rid}: {res.stderr.strip()}")

print(f"\nDone: {deleted} deleted, {failed} failed.")
