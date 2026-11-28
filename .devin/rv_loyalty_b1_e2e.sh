#!/bin/bash
# RV Batch 1 E2E — checkout -> complete order -> verify loyalty award (PG) + mirror sync event
set +e
BASE="https://app2.khachvip.online"
API="https://api2.khachvip.online"
COOKIE=/tmp/rv_lp_cookies.txt
rm -f $COOKIE
TS=$(date +%s)
DEVICE="b1$(date +%s)00-0000-4000-8000-$(date +%s | tail -c 9)000000"

echo "=== Step 1: Login SystemAdmin ==="
curl -s -i -c $COOKIE -X POST "$BASE/Login" -d 'Username=sysadmin@vanan.vn&Password=2026@vanan&RememberMe=false' -k --max-redirs 0 2>&1 | grep -E 'HTTP/' | head -1

TENANT_ID="00000000-0000-0000-0000-000000000001"
echo ""
echo "=== Step 2: Impersonate tenant $TENANT_ID ==="
curl -s -b $COOKIE -c $COOKIE -X POST "$BASE/api/admin/impersonate/${TENANT_ID}" -H 'Content-Type: application/json' -k | head -c 300
echo ""

echo ""
echo "=== Step 3: Checkout order ==="
CHECKOUT_RESP=$(curl -s -X POST "$API/api/public/orders/checkout" -H 'Content-Type: application/json' -k \
  -d "{\"customerDeviceId\":\"$DEVICE\",\"customerName\":\"RV LP B1\",\"customerPhone\":\"0909$TS\",\"customerAddress\":\"RV 1 Test St\",\"customerNotes\":\"RV loyalty B1 ts=$TS\",\"orderType\":\"DINEIN\",\"items\":[{\"productId\":\"00000000-0000-0000-0000-000000000001\",\"tenantId\":\"$TENANT_ID\",\"productName\":\"Cafe Sua Da\",\"quantity\":2,\"unitPrice\":25000,\"vatRate\":0.10}]}")
echo "$CHECKOUT_RESP" | head -c 600
echo ""
ORDER_ID=$(echo "$CHECKOUT_RESP" | python3 -c "import sys,json; d=json.load(sys.stdin); o=d.get('orders',[]); print(o[0].get('orderId') if o else '')" 2>/dev/null)
echo "OrderId: $ORDER_ID"
if [ -z "$ORDER_ID" ]; then echo "CHECKOUT FAILED"; exit 1; fi

echo ""
echo "=== Step 4: Wait 15s NATS sync → SQLite ==="
sleep 15

echo ""
echo "=== Step 5: Transition pending → completed ==="
for STATUS in confirmed preparing ready delivered completed; do
  RESP=$(curl -s -w '\nHTTP_CODE:%{http_code}' -X PUT "$BASE/api/orderworkflow/${ORDER_ID}/status" \
    -H 'Content-Type: application/json' -b $COOKIE -c $COOKIE -k -d "{\"Status\":\"$STATUS\",\"Reason\":\"RV LP B1 ts=$TS\"}")
  HTTP_CODE=$(echo "$RESP" | grep 'HTTP_CODE:' | cut -d: -f2)
  echo "  -> $STATUS: HTTP $HTTP_CODE"
done

echo ""
echo "=== Step 6: Order details ==="
curl -s "$BASE/api/orderworkflow/${ORDER_ID}" -b $COOKIE -k | python3 -c "
import sys,json
d=json.load(sys.stdin)
print('status:', d.get('status',{}).get('value'))
print('customerId:', d.get('customerId'))
print('customerDeviceId:', d.get('customerDeviceId'))
print('totalAmount:', d.get('totalAmount'))
" 2>/dev/null

echo ""
echo "=== Step 7: PG — LoyaltyIssuanceRecord + LoyaltyRewards cho order này ==="
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "
SELECT 'issuance:', substring(\"OrderId\"::text,1,8), substring(\"CustomerId\"::text,1,8), \"PointsIssued\"
FROM \"LoyaltyIssuanceRecords\" WHERE \"OrderId\" = '$ORDER_ID';"
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "
SELECT 'pg_rewards:', substring(\"CustomerId\"::text,1,8), substring(\"TenantId\"::text,1,8), \"PointBalance\", \"UpdatedAt\"
FROM \"LoyaltyRewards\" WHERE \"CustomerId\" IN (SELECT \"CustomerId\" FROM \"Orders\" WHERE \"Id\" = '$ORDER_ID');"

echo ""
echo "=== Step 8: Gateway logs — LoyaltyBalanceSync sau checkout ==="
docker logs vanan-gateway-1 --since 5m 2>&1 | grep -E "LoyaltyBalanceSync|LOYALTY: Awarded|Skipped duplicate" | tail -8

echo ""
echo "=== OUTPUT ==="
echo "ORDER_ID=$ORDER_ID"
echo "DEVICE=$DEVICE"
echo "TENANT_ID=$TENANT_ID"
