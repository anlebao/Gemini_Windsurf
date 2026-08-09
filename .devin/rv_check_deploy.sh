#!/bin/bash
echo "=== DLL freshness ==="
NOW=$(date +%s)
ERP_TIME=$(docker exec vanan-shoperp stat -c '%Y' /app/VanAn.ShopERP.dll 2>/dev/null || echo 0)
AGE_ERP=$(( (NOW - ERP_TIME) / 60 ))
echo "  ShopERP DLL age: ${AGE_ERP} min"

echo ""
echo "=== Check if new code deployed (TenantId value object pattern) ==="
docker cp vanan-shoperp:/app/VanAn.ShopERP.dll /tmp/verp_check.dll 2>/dev/null
python3 -c "
data = open('/tmp/verp_check.dll', 'rb').read()
# Old pattern: 'TenantId.Value' should be gone, new pattern: 'new TenantId' should be present
print('  new TenantId (utf-16):', data.count('new TenantId'.encode('utf-16-le')))
"

echo ""
echo "=== Latest error log ==="
docker logs vanan-shoperp --tail 50 2>&1 | grep -A5 "loyalty\|dashboard\|GetDashboard\|InvalidOperationException\|Exception" | tail -20
