#!/usr/bin/env python3
import subprocess, re, sys, os

DOMAIN = os.environ.get("VANAN_DOMAIN", "localhost")
BASE = f"https://{DOMAIN}"

r = subprocess.run(
    ["curl", "-sk", f"{BASE}/Login", "-c", "/tmp/jar.txt"],
    capture_output=True, text=True
)
m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', r.stdout)
if not m:
    m = re.search(r'value="([^"]+)"[^>]*name="__RequestVerificationToken"', r.stdout)
if not m:
    print("No CSRF. Status check:")
    print(r.stdout[:300])
    sys.exit(1)
token = m.group(1)

r2 = subprocess.run(
    ["curl", "-sk", "-X", "POST",
     f"{BASE}/Login?ReturnUrl=%2F",
     "-b", "/tmp/jar.txt", "-D", "-", "-o", "/dev/null",
     "--data-urlencode", "Username=adminvanan1",
     "--data-urlencode", "Password=2026@vanan",
     "-d", f"__RequestVerificationToken={token}"],
    capture_output=True, text=True
)
print(r2.stdout[:200])
if "302" in r2.stdout or "location:" in r2.stdout.lower():
    print("\nSUCCESS: Login OK!")
else:
    print("\nFAIL: Still no redirect.")

import subprocess as sp
log = sp.run(["docker", "logs", "vanan-shoperp", "--since", "30s"],
             capture_output=True, text=True)
for l in (log.stdout + log.stderr).splitlines():
    if any(k in l for k in ["Login", "found=", "BCrypt", "Exception", "WRN", "ERR"]):
        print(l)
