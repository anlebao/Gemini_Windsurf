#!/bin/bash
rm -f /tmp/rv_cookies3.txt

echo "=== Login ==="
curl -s -i -c /tmp/rv_cookies3.txt -X POST 'https://app.khachvip.online/Login' \
  -d 'Username=sysadmin@vanan.vn&Password=2026@vanan&RememberMe=false' -k --max-redirs 0 2>&1 | grep -E 'HTTP/|Set-Cookie: \.VanAn\.Auth' | head -2

TENANT_ID="00000000-0000-0000-0000-000000000001"
ORDER_ID="019fa22a-eade-7194-a248-3d01328345e0"

echo ""
echo "=== Impersonate (capture all Set-Cookie) ==="
curl -s -i -b /tmp/rv_cookies3.txt -c /tmp/rv_cookies3.txt -X POST "https://app.khachvip.online/api/admin/impersonate/${TENANT_ID}" \
  -H 'Content-Type: application/json' -k 2>&1 | grep -iE 'Set-Cookie|HTTP/' | head -5

echo ""
echo "=== Cookies after impersonate ==="
grep -i 'VanAn.Auth' /tmp/rv_cookies3.txt | head -2

echo ""
echo "=== Now transition delivered -> completed (using cookies, no Bearer) ==="
curl -s -w '\nHTTP_CODE:%{http_code}\n' -X PUT "https://app.khachvip.online/api/orderworkflow/${ORDER_ID}/status" \
  -H 'Content-Type: application/json' \
  -b /tmp/rv_cookies3.txt -c /tmp/rv_cookies3.txt -k \
  -d '{"Status":"completed","Reason":"RV test Bug 6 - verify loyalty"}'

echo ""
echo "=== Verify order status ==="
curl -s "https://app.khachvip.online/api/orderworkflow/${ORDER_ID}" \
  -b /tmp/rv_cookies3.txt -k | python3 -c "import sys,json; d=json.load(sys.stdin); print('status:', d.get('status',{}).get('value')); print('completedAt:', d.get('completedAt'))" 2>/dev/null
