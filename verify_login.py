#!/usr/bin/env python3
import subprocess, re, sys

# GET /Login để lấy CSRF token + cookie
r = subprocess.run(
    ["curl", "-sk", "https://vanantech.io.vn/Login", "-c", "/tmp/jar.txt"],
    capture_output=True, text=True
)
m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', r.stdout)
if not m:
    m = re.search(r'value="([^"]+)"[^>]*name="__RequestVerificationToken"', r.stdout)
if not m:
    print("ERROR: no CSRF token")
    sys.exit(1)
token = m.group(1)
print(f"Token: {token[:30]}...")

# POST /Login
r2 = subprocess.run(
    ["curl", "-sk", "-X", "POST",
     "https://vanantech.io.vn/Login?ReturnUrl=%2F",
     "-b", "/tmp/jar.txt", "-D", "-", "-o", "/dev/null",
     "--data-urlencode", "Username=adminvanan1",
     "--data-urlencode", "Password=2026@vanan",
     "-d", f"__RequestVerificationToken={token}"],
    capture_output=True, text=True
)
print("Response headers:")
print(r2.stdout[:300])
if "302" in r2.stdout or "location:" in r2.stdout.lower():
    print("\nSUCCESS: Login OK!")
else:
    print("\nFAIL: No redirect.")
