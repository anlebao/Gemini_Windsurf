import subprocess
r = subprocess.run(["docker", "exec", "vanan-shoperp", "env"], capture_output=True, text=True)
for l in r.stdout.splitlines():
    if any(k in l.upper() for k in ["DB", "DATA", "SQL", "CONNECT", "POSTGRES", "SQLITE", "CONNECTION"]):
        print(l)
