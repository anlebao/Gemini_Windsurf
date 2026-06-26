#!/usr/bin/env python3
import subprocess, re, sys

BASE = "http://172.18.0.7"

r = subprocess.run(
    ["curl", "-sk", f"{BASE}/Login", "-c", "/tmp/jar2.txt"],
    capture_output=True, text=True
)
m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', r.stdout)
if not m:
    m = re.search(r'value="([^"]+)"[^>]*name="__RequestVerificationToken"', r.stdout)
if not m:
    print("No CSRF token found")
    sys.exit(1)
token = m.group(1)
print(f"CSRF OK: {token[:20]}...")

r2 = subprocess.run(
    ["curl", "-sk", "-X", "POST", f"{BASE}/Login?ReturnUrl=%2F",
     "-b", "/tmp/jar2.txt", "-D", "-", "-o", "/dev/null",
     "--data-urlencode", "Username=adminvanan1",
     "--data-urlencode", "Password=2026@vanan",
     "-d", f"__RequestVerificationToken={token}"],
    capture_output=True, text=True
)
print(r2.stdout[:300])
if "302" in r2.stdout or "location:" in r2.stdout.lower():
    print("\nSUCCESS: Login OK!")
else:
    print("\nFAIL: No redirect")
