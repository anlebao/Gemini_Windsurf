#!/bin/bash
python3 << 'PYEOF'
import os
dll_path = '/tmp/verp_phaseA.dll'
if not os.path.exists(dll_path):
    # Copy fresh from container
    import subprocess
    subprocess.run(['docker', 'cp', 'vanan-shoperp:/app/VanAn.ShopERP.dll', dll_path], stderr=subprocess.DEVNULL)
data = open(dll_path, 'rb').read()
checks = [
    ('Thống kê điểm thưởng', 'utf-8'),
    ('Thống kê điểm thưởng', 'utf-16-le'),
    ('loyalty/dashboard', 'utf-8'),
    ('loyalty/dashboard', 'utf-16-le'),
    ('bar-chart-fill', 'utf-8'),
    ('bar-chart-fill', 'utf-16-le'),
    ('LoyaltyDashboard', 'utf-8'),
    ('LoyaltyDashboard', 'utf-16-le'),
]
for s, enc in checks:
    print(f'  {s} ({enc}): {data.count(s.encode(enc))} matches')
PYEOF
