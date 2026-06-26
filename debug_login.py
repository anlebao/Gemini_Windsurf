#!/usr/bin/env python3
# Kiểm tra xem app có thể đọc user không bằng cách gọi raw EF query
import subprocess

# Check log với level chi tiết hơn bằng cách trigger và xem full log kể cả stderr
r = subprocess.run(
    ["docker", "logs", "vanan-shoperp", "--since", "3m"],
    capture_output=True, text=True
)
# Tìm bất kỳ exception nào
lines = r.stderr.split('\n') + r.stdout.split('\n')
for l in lines:
    if any(k in l for k in ['Exception', 'Error', 'WARN', 'fail', 'Unhandled', 'CryptographicException']):
        print(l)
