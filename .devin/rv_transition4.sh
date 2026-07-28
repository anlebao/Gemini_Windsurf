#!/bin/bash
JWT=$(grep '.VanAn.Jwt' /tmp/rv_cookies.txt | awk '{print $NF}')
ORDER_ID="019fa22a-eade-7194-a248-3d01328345e0"

# Decode JWT payload
echo "=== JWT tenant_id claim ==="
echo "$JWT" | cut -d. -f2 | base64 -d 2>/dev/null | python3 -c "import sys,json; d=json.load(sys.stdin); print('tenant_id:', d.get('tenant_id')); print('TenantId:', d.get('TenantId'))" 2>/dev/null || echo "$JWT" | cut -d. -f2 | base64 -d 2>/dev/null

echo ""
echo "=== Order detail via GET /api/orderworkflow/{id} ==="
curl -s "https://app.khachvip.online/api/orderworkflow/${ORDER_ID}" \
  -H "Authorization: Bearer $JWT" -k | python3 -c "import sys,json; d=json.load(sys.stdin); print('status:', d.get('status',{}).get('value')); print('tenantId:', d.get('tenantId',{}).get('value')); print('customerId:', d.get('customerId')); print('customerDeviceId:', d.get('customerDeviceId'))" 2>/dev/null

echo ""
echo "=== Try confirm-received endpoint (ready->delivered, but order already delivered) ==="
echo "=== Try direct completed via OrdersController with reason ==="
curl -s -w '\nHTTP_CODE:%{http_code}\n' -X PUT "https://app.khachvip.online/api/orders/${ORDER_ID}/status" \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $JWT" \
  -b /tmp/rv_cookies.txt -k \
  -d '{"Status":"completed","Reason":"RV test Bug 6 - verify loyalty"}'
