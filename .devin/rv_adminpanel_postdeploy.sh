#!/bin/bash
# RV post-deploy: admin panel fix (eligible list + activate) + PII repair + crawl error surfacing
set +e
API="https://api2.khachvip.online"
TEST_CUSTOMER="d3373f11-f9de-46c1-b8d5-98d5937cc6d6"  # "RV B2 Ledger A"

echo "=== Step 1: Login SystemAdmin ==="
TOKEN=$(curl -sk -X POST $API/api/platform/login -H 'Content-Type: application/json' \
  -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' \
  | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('token',''))" 2>/dev/null)
[ -z "$TOKEN" ] && echo "LOGIN_FAILED" && exit 1
echo "LOGIN_OK"

echo ""
echo "=== Step 2: GET eligible includeIneligible=true (was 500) ==="
curl -sk -w '\nHTTP:%{http_code}\n' "$API/api/admin/community/eligible?page=1&pageSize=5&includeIneligible=true" \
  -H "Authorization: Bearer $TOKEN" | head -c 900

echo ""
echo "=== Step 3: POST activate-role bypass=true on test customer (was 500) ==="
ACT=$(curl -sk -w '\nHTTP:%{http_code}' -X POST "$API/api/admin/community/$TEST_CUSTOMER/activate-role" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"Role":"Salesman","BypassEligibility":true}')
echo "$ACT"

echo ""
echo "=== Step 4: Deactivate (cleanup) ==="
curl -sk -w '\nHTTP:%{http_code}\n' -X POST "$API/api/admin/community/$TEST_CUSTOMER/deactivate-role" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{"Role":"Salesman"}'

echo ""
echo "=== Step 5: POST pii-repair (re-encrypt lost-key rows) ==="
curl -sk -w '\nHTTP:%{http_code}\n' -X POST "$API/api/admin/community/pii-repair" \
  -H "Authorization: Bearer $TOKEN"

echo ""
echo "=== Step 6: Re-test eligible + activate AFTER repair ==="
curl -sk -o /dev/null -w 'eligible-after: %{http_code}\n' "$API/api/admin/community/eligible?page=1&pageSize=5&includeIneligible=true" \
  -H "Authorization: Bearer $TOKEN"
curl -sk -o /dev/null -w 'activate-after: %{http_code}\n' -X POST "$API/api/admin/community/$TEST_CUSTOMER/activate-role" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"Role":"Shipper","BypassEligibility":true}'
curl -sk -o /dev/null -w 'deactivate-after: %{http_code}\n' -X POST "$API/api/admin/community/$TEST_CUSTOMER/deactivate-role" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{"Role":"Shipper"}'

echo ""
echo "=== Step 7: Crawl status (error surfacing) ==="
curl -sk "$API/api/v1/crawl/status" -H "Authorization: Bearer $TOKEN" | head -c 600
echo ""
