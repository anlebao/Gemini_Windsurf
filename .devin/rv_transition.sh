#!/bin/bash
JWT=$(grep '.VanAn.Jwt' /tmp/rv_cookies.txt | awk '{print $NF}')
TENANT_ID="00000000-0000-0000-0000-000000000001"
ORDER_ID="019fa22a-eade-7194-a248-3d01328345e0"

echo "=== Step 1: Impersonate tenant ${TENANT_ID} ==="
IMP_RESP=$(curl -s -w '\nHTTP_CODE:%{http_code}' -X POST "https://app.khachvip.online/api/admin/impersonate/${TENANT_ID}" \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $JWT" \
  -b /tmp/rv_cookies.txt -c /tmp/rv_cookies.txt -k)
echo "$IMP_RESP"

# Extract new JWT from response
NEW_JWT=$(echo "$IMP_RESP" | grep -o '"token":"[^"]*"' | sed 's/"token":"//;s/"$//')
if [ -n "$NEW_JWT" ]; then
  JWT="$NEW_JWT"
  echo "Got new impersonated JWT (length: ${#JWT})"
fi

echo ""
echo "=== Step 2: Transition pending -> confirmed ==="
curl -s -w '\nHTTP_CODE:%{http_code}\n' -X PUT "https://app.khachvip.online/api/orderworkflow/${ORDER_ID}/status" \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $JWT" \
  -b /tmp/rv_cookies.txt -k \
  -d '{"Status":"confirmed"}'

echo ""
echo "=== Step 3: Transition confirmed -> preparing ==="
curl -s -w '\nHTTP_CODE:%{http_code}\n' -X PUT "https://app.khachvip.online/api/orderworkflow/${ORDER_ID}/status" \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $JWT" \
  -b /tmp/rv_cookies.txt -k \
  -d '{"Status":"preparing"}'

echo ""
echo "=== Step 4: Transition preparing -> ready ==="
curl -s -w '\nHTTP_CODE:%{http_code}\n' -X PUT "https://app.khachvip.online/api/orderworkflow/${ORDER_ID}/status" \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $JWT" \
  -b /tmp/rv_cookies.txt -k \
  -d '{"Status":"ready"}'

echo ""
echo "=== Step 5: Transition ready -> completed ==="
curl -s -w '\nHTTP_CODE:%{http_code}\n' -X PUT "https://app.khachvip.online/api/orderworkflow/${ORDER_ID}/status" \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $JWT" \
  -b /tmp/rv_cookies.txt -k \
  -d '{"Status":"completed"}'
