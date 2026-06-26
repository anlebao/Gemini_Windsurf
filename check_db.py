import subprocess
r = subprocess.run(
    ["docker", "exec", "vanan-postgres", "psql",
     "-U", "vanan_admin", "-d", "VanAnCoreHub",
     "-c", 'SELECT "Id", "Username", "IsActive", "IsDeleted" FROM "Users";'],
    capture_output=True, text=True
)
print(r.stdout)
print(r.stderr[:200] if r.stderr else "")
