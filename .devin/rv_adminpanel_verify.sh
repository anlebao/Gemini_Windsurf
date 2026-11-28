#!/bin/bash
# RV: verify 2 admin bugs — eligible list + activate-role (production)
set +e
API="https://api2.khachvip.online"

echo "=== Step 1: Login SystemAdmin ==="
TOKEN=$(curl -sk -X POST $API/api/platform/login \
  -H 'Content-Type: application/json' \
  -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' \
  | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('token',''))" 2>/dev/null)
if [ -z "$TOKEN" ]; then echo "LOGIN_FAILED"; exit 1; fi
echo "LOGIN_OK (token len: ${#TOKEN})"

echo ""
echo "=== Step 2: GET eligible (default — criteria Verified+1000pts) ==="
RESP=$(curl -sk -w '\nHTTP:%{http_code}' "$API/api/admin/community/eligible?page=1&pageSize=20" \
  -H "Authorization: Bearer $TOKEN")
echo "$RESP" | head -c 600; echo ""

echo ""
echo "=== Step 3: GET eligible includeIneligible=true ==="
RESP2=$(curl -sk -w '\nHTTP:%{http_code}' "$API/api/admin/community/eligible?page=1&pageSize=20&includeIneligible=true" \
  -H "Authorization: Bearer $TOKEN")
echo "$RESP2" | head -c 600; echo ""

echo ""
echo "=== Step 4: POST activate-role (bypass=true) on test customer, then deactivate ==="
# Use a dedicated RV test customer id (created in prior RV sessions, low risk)
CUSTOMER_ID="d3373f11-f9de-46c1-b8d5-98d5937cc6d6"   # "RV B2 Ledger A" — test customer
ACT=$(curl -sk -w '\nHTTP:%{http_code}' -X POST "$API/api/admin/community/$CUSTOMER_ID/activate-role" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"Role":"Salesman","BypassEligibility":true}')
echo "Activate: $ACT"; echo ""
DEACT=$(curl -sk -w '\nHTTP:%{http_code}' -X POST "$API/api/admin/community/$CUSTOMER_ID/deactivate-role" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"Role":"Salesman"}')
echo "Deactivate (cleanup): $DEACT"; echo ""
