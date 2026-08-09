#!/bin/bash
docker cp vanan-shoperp:/app/VanAn.ShopERP.dll /tmp/verp_106.dll 2>/dev/null
docker cp vanan-shoperp:/app/VanAn.CoreHub.dll /tmp/vcore_106.dll 2>/dev/null

python3 -c "
shop = open('/tmp/verp_106.dll', 'rb').read()
core = open('/tmp/vcore_106.dll', 'rb').read()
checks = [
    ('CustomerMergeService', 'utf-16-le'),
    ('MergeDeviceStubsIntoLoginAsync', 'utf-16-le'),
    ('ICustomerMergeService', 'utf-16-le'),
    ('CustomerMergeResult', 'utf-16-le'),
]
print('=== ShopERP DLL ===')
for s, enc in checks:
    print(f'  {s}: {shop.count(s.encode(enc))} matches')
print('=== CoreHub DLL ===')
for s, enc in checks:
    print(f'  {s}: {core.count(s.encode(enc))} matches')
"
