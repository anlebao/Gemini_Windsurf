#!/bin/bash
set -e

TENANT_ID="81e168d4-e44a-4728-a1ea-55151b168c96"

echo "=== Step 1: Platform Login (via nginx HTTPS) ==="
LOGIN_RESP=$(curl -sk -X POST https://app.khachvip.online/api/platform/login \
  -H "Content-Type: application/json" \
  -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' \
  -c /tmp/sysadmin_cookies.txt \
  -D /tmp/login_headers.txt \
  -w "\nHTTP_STATUS:%{http_code}")
echo "$LOGIN_RESP"

echo ""
echo "=== Set-Cookie headers from login ==="
grep -i "set-cookie" /tmp/login_headers.txt || echo "(none)"

echo ""
echo "=== Cookies saved ==="
cat /tmp/sysadmin_cookies.txt

echo ""
echo "=== Step 2: Impersonate tenant $TENANT_ID ==="
IMP_RESP=$(curl -sk -X POST "https://app.khachvip.online/api/admin/impersonate/$TENANT_ID" \
  -H "Content-Type: application/json" \
  -b /tmp/sysadmin_cookies.txt \
  -c /tmp/sysadmin_cookies.txt \
  -D /tmp/impersonate_headers.txt \
  -w "\nHTTP_STATUS:%{http_code}")
echo "$IMP_RESP"

echo ""
echo "=== Impersonate response headers ==="
head -10 /tmp/impersonate_headers.txt

echo ""
echo "=== Step 3: Verify Gateway GET /api/v1/tenants/{id} (from host) ==="
# Extract JWT from login response for Gateway call
JWT=$(echo "$LOGIN_RESP" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "JWT (first 50 chars): ${JWT:0:50}..."
TENANT_RESP=$(curl -sk "https://api.khachvip.online/api/v1/tenants/$TENANT_ID" \
  -H "Authorization: Bearer $JWT" \
  -w "\nHTTP_STATUS:%{http_code}")
echo "$TENANT_RESP"
