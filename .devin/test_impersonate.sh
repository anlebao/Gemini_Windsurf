#!/bin/bash
set -e

echo "=== Step 1: Platform Login ==="
LOGIN_RESP=$(curl -s -X POST http://localhost:80/api/platform/login \
  -H "Content-Type: application/json" \
  -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' \
  -c /tmp/sysadmin_cookies.txt \
  -w "\nHTTP_STATUS:%{http_code}")
echo "$LOGIN_RESP"

echo ""
echo "=== Cookies saved ==="
cat /tmp/sysadmin_cookies.txt

echo ""
echo "=== Step 2: Impersonate tenant 81e168d4-e44a-4728-a1ea-55151b168c96 ==="
IMP_RESP=$(curl -s -X POST http://localhost:80/api/admin/impersonate/81e168d4-e44a-4728-a1ea-55151b168c96 \
  -H "Content-Type: application/json" \
  -b /tmp/sysadmin_cookies.txt \
  -c /tmp/sysadmin_cookies.txt \
  -w "\nHTTP_STATUS:%{http_code}")
echo "$IMP_RESP"

echo ""
echo "=== Step 3: Verify Gateway GET /api/v1/tenants/{id} ==="
TENANT_RESP=$(curl -s http://localhost:80/api/v1/tenants/81e168d4-e44a-4728-a1ea-55151b168c96 \
  -b /tmp/sysadmin_cookies.txt \
  -w "\nHTTP_STATUS:%{http_code}")
echo "$TENANT_RESP"
