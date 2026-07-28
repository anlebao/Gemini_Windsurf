#!/bin/bash
# Full Bug 6 verification: login -> impersonate -> checkout -> transition to completed -> verify loyalty
rm -f /tmp/rv_cookies4.txt

echo "=== Step 1: Login as SystemAdmin ==="
curl -s -i -c /tmp/rv_cookies4.txt -X POST 'https://app.khachvip.online/Login' \
  -d 'Username=sysadmin@vanan.vn&Password=2026@vanan&RememberMe=false' -k --max-redirs 0 2>&1 | grep -E 'HTTP/' | head -1

TENANT_ID="00000000-0000-0000-0000-000000000001"

echo ""
echo "=== Step 2: Impersonate tenant ==="
curl -s -b /tmp/rv_cookies4.txt -c /tmp/rv_cookies4.txt -X POST "https://app.khachvip.online/api/admin/impersonate/${TENANT_ID}" \
  -H 'Content-Type: application/json' -k | python3 -c "import sys,json; d=json.load(sys.stdin); print('success:', d.get('success'), 'tenant:', d.get('tenantName'))" 2>/dev/null

echo ""
echo "=== Step 3: Checkout new order (via Gateway api.khachvip.online) ==="
CHECKOUT_RESP=$(curl -s -X POST "https://api.khachvip.online/api/public/orders/checkout" \
  -H 'Content-Type: application/json' -k \
  -d '{"customerDeviceId":"b2c3d4e5-f6a7-8901-bcde-f23456789012","customerName":"RV Bug6 V2","customerPhone":"0909987654","customerAddress":"456 Test Ave","customerNotes":"RV Bug6 V2 - loyalty end-to-end","orderType":"DINEIN","items":[{"productId":"00000000-0000-0000-0000-000000000001","tenantId":"00000000-0000-0000-0000-000000000001","productName":"Cafe Sua Da","quantity":3,"unitPrice":25000,"vatRate":0.10}]}')
echo "$CHECKOUT_RESP" | python3 -c "import sys,json; d=json.load(sys.stdin); print('successCount:', d.get('successCount')); o=d.get('orders',[]); print('orderId:', o[0].get('orderId') if o else 'none')" 2>/dev/null
ORDER_ID=$(echo "$CHECKOUT_RESP" | python3 -c "import sys,json; d=json.load(sys.stdin); o=d.get('orders',[]); print(o[0].get('orderId') if o else '')" 2>/dev/null)
echo "Order ID: $ORDER_ID"

if [ -z "$ORDER_ID" ]; then
  echo "ERROR: No order ID. Full response:"
  echo "$CHECKOUT_RESP"
  exit 1
fi

echo ""
echo "=== Step 4: Wait 15s for NATS sync to SQLite ==="
sleep 15

echo ""
echo "=== Step 5: Transition pending -> confirmed -> preparing -> ready -> delivered -> completed ==="
for STATUS in confirmed preparing ready delivered completed; do
  echo -n "  -> $STATUS: "
  RESP=$(curl -s -w '\nHTTP_CODE:%{http_code}' -X PUT "https://app.khachvip.online/api/orderworkflow/${ORDER_ID}/status" \
    -H 'Content-Type: application/json' \
    -b /tmp/rv_cookies4.txt -c /tmp/rv_cookies4.txt -k \
    -d "{\"Status\":\"$STATUS\",\"Reason\":\"RV Bug6 V2 verify\"}")
  HTTP_CODE=$(echo "$RESP" | grep 'HTTP_CODE:' | cut -d: -f2)
  NEW_STATUS=$(echo "$RESP" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('status',{}).get('value','?'))" 2>/dev/null || echo "?")
  echo "HTTP $HTTP_CODE, status=$NEW_STATUS"
done

echo ""
echo "=== Step 6: Verify order status + completedAt ==="
curl -s "https://app.khachvip.online/api/orderworkflow/${ORDER_ID}" \
  -b /tmp/rv_cookies4.txt -k | python3 -c "import sys,json; d=json.load(sys.stdin); print('status:', d.get('status',{}).get('value')); print('completedAt:', d.get('completedAt')); print('customerId:', d.get('customerId')); print('customerDeviceId:', d.get('customerDeviceId'))" 2>/dev/null

echo ""
echo "=== Step 7: Check ShopERP logs for loyalty award ==="
docker logs vanan-shoperp --since 60s 2>&1 | grep -iE "LOYALTY|Bug 6 fix|customer stub|AddPoints|Failed to" | tail -10
