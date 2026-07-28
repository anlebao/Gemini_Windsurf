#!/bin/bash
JWT=$(grep '.VanAn.Jwt' /tmp/rv_cookies.txt | awk '{print $NF}')
ORDER_ID="019fa22a-eade-7194-a248-3d01328345e0"

# Order is now "delivered". Check valid transitions from delivered.
echo "=== Valid transitions from delivered ==="
for next in completed cancelled; do
  echo -n "delivered -> $next: "
  curl -s "https://app.khachvip.online/api/orderworkflow/transition-valid?current=delivered&next=$next" \
    -H "Authorization: Bearer $JWT" -k
  echo ""
done

# Try the OrdersController endpoint instead: PUT /api/orders/{id}/status
echo ""
echo "=== Try PUT /api/orders/{id}/status with completed ==="
curl -s -w '\nHTTP_CODE:%{http_code}\n' -X PUT "https://app.khachvip.online/api/orders/${ORDER_ID}/status" \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $JWT" \
  -b /tmp/rv_cookies.txt -k \
  -d '{"Status":"completed"}'
