#!/bin/bash
# Full flow: login -> impersonate -> transition to completed
rm -f /tmp/rv_cookies2.txt

echo "=== Step 1: Login as SystemAdmin ==="
curl -s -i -c /tmp/rv_cookies2.txt -X POST 'https://app.khachvip.online/Login' \
  -d 'Username=sysadmin@vanan.vn&Password=2026@vanan&RememberMe=false' -k --max-redirs 0 2>&1 | grep -E 'HTTP/|Set-Cookie: .VanAn.Jwt' | head -3

TENANT_ID="00000000-0000-0000-0000-000000000001"
ORDER_ID="019fa22a-eade-7194-a248-3d01328345e0"

echo ""
echo "=== Step 2: Impersonate tenant ==="
IMP_RESP=$(curl -s -X POST "https://app.khachvip.online/api/admin/impersonate/${TENANT_ID}" \
  -H 'Content-Type: application/json' \
  -b /tmp/rv_cookies2.txt -c /tmp/rv_cookies2.txt -k)
echo "$IMP_RESP" | python3 -c "import sys,json; d=json.load(sys.stdin); print('success:', d.get('success'), 'tenant:', d.get('tenantName'))" 2>/dev/null

# Extract new JWT
JWT=$(echo "$IMP_RESP" | python3 -c "import sys,json; print(json.load(sys.stdin).get('token',''))" 2>/dev/null)
echo "New JWT length: ${#JWT}"

echo ""
echo "=== Verify JWT tenant_id ==="
echo "$JWT" | cut -d. -f2 | base64 -d 2>/dev/null | python3 -c "import sys,json; d=json.load(sys.stdin); print('tenant_id:', d.get('tenant_id'))" 2>/dev/null

echo ""
echo "=== Step 3: Transition delivered -> completed ==="
RESP=$(curl -s -w '\nHTTP_CODE:%{http_code}' -X PUT "https://app.khachvip.online/api/orderworkflow/${ORDER_ID}/status" \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $JWT" \
  -b /tmp/rv_cookies2.txt -k \
  -d '{"Status":"completed","Reason":"RV test Bug 6 - verify loyalty"}')
echo "$RESP" | tail -5

echo ""
echo "=== Verify order status ==="
curl -s "https://app.khachvip.online/api/orderworkflow/${ORDER_ID}" \
  -H "Authorization: Bearer $JWT" -k | python3 -c "import sys,json; d=json.load(sys.stdin); print('status:', d.get('status',{}).get('value')); print('completedAt:', d.get('completedAt'))" 2>/dev/null
