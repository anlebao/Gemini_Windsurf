#!/bin/bash
JWT=$(grep '.VanAn.Jwt' /tmp/rv_cookies.txt | awk '{print $NF}')
ORDER_ID="019fa22a-eade-7194-a248-3d01328345e0"

echo "=== Valid transitions from ready ==="
for next in completed delivered cancelled; do
  echo -n "ready -> $next: "
  curl -s "https://app.khachvip.online/api/orderworkflow/transition-valid?current=ready&next=$next" \
    -H "Authorization: Bearer $JWT" -k
  echo ""
done

echo ""
echo "=== Try ready -> delivered (then delivered -> completed) ==="
curl -s -w '\nHTTP_CODE:%{http_code}\n' -X PUT "https://app.khachvip.online/api/orderworkflow/${ORDER_ID}/status" \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $JWT" \
  -b /tmp/rv_cookies.txt -k \
  -d '{"Status":"delivered"}'

echo ""
echo "=== Try delivered -> completed ==="
curl -s -w '\nHTTP_CODE:%{http_code}\n' -X PUT "https://app.khachvip.online/api/orderworkflow/${ORDER_ID}/status" \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $JWT" \
  -b /tmp/rv_cookies.txt -k \
  -d '{"Status":"completed"}'
