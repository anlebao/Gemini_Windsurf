#!/bin/bash
# RV Batch 1 E2E v2 — checkout (tenant loyalty ON) -> complete qua GATEWAY -> verify PG award + sync
set +e
BASE="https://app2.khachvip.online"
API="https://api2.khachvip.online"
COOKIE=/tmp/rv_lp2_cookies.txt
rm -f $COOKIE
TS=$(date +%s)
TENANT_ID="0dfab177-86ad-4513-bd69-ddf956c7b768"   # "Vạn An Test" — Active + loyalty enabled=True
DEVICE=$(python3 -c "import uuid; print(uuid.uuid4())")

echo "=== Step 1: Login + impersonate tenant $TENANT_ID ==="
curl -s -i -c $COOKIE -X POST "$BASE/Login" -d 'Username=sysadmin@vanan.vn&Password=2026@vanan&RememberMe=false' -k --max-redirs 0 2>&1 | grep -E 'HTTP/' | head -1
IMP=$(curl -s -b $COOKIE -c $COOKIE -X POST "$BASE/api/admin/impersonate/${TENANT_ID}" -H 'Content-Type: application/json' -k)
echo "$IMP" | head -c 200; echo ""
TOKEN=$(echo "$IMP" | python3 -c "import sys,json; print(json.load(sys.stdin).get('token',''))" 2>/dev/null)
[ -z "$TOKEN" ] && echo "IMPERSONATE FAILED" && exit 1

echo ""
echo "=== Step 2: Checkout (guest, device=$DEVICE) ==="
CHECKOUT_RESP=$(curl -s -X POST "$API/api/public/orders/checkout" -H 'Content-Type: application/json' -k \
  -d "{\"customerDeviceId\":\"$DEVICE\",\"customerName\":\"RV LP B1v2\",\"customerPhone\":\"0909$TS\",\"customerAddress\":\"RV 2 Test St\",\"customerNotes\":\"RV loyalty B1v2 ts=$TS\",\"orderType\":\"DINEIN\",\"items\":[{\"productId\":\"00000000-0000-0000-0000-000000000001\",\"tenantId\":\"$TENANT_ID\",\"productName\":\"Cafe Sua Da\",\"quantity\":2,\"unitPrice\":25000,\"vatRate\":0.10}]}")
echo "$CHECKOUT_RESP" | head -c 500; echo ""
ORDER_ID=$(echo "$CHECKOUT_RESP" | python3 -c "import sys,json; d=json.load(sys.stdin); o=d.get('orders',[]); print(o[0].get('orderId') if o else '')" 2>/dev/null)
echo "OrderId: $ORDER_ID"
[ -z "$ORDER_ID" ] && echo "CHECKOUT FAILED" && exit 1

echo ""
echo "=== Step 3: Wait 15s NATS sync → SQLite ==="
sleep 15

echo ""
echo "=== Step 4: Transition qua GATEWAY api2/api/orders/{id}/status (Bearer) ==="
for STATUS in confirmed preparing ready delivered completed; do
  RESP=$(curl -s -w '\nHTTP_CODE:%{http_code}' -X PUT "$API/api/orders/${ORDER_ID}/status" \
    -H 'Content-Type: application/json' -H "Authorization: Bearer $TOKEN" -k \
    -d "{\"Status\":\"$STATUS\",\"Reason\":\"RV LP B1v2 ts=$TS\"}")
  HTTP_CODE=$(echo "$RESP" | grep 'HTTP_CODE:' | cut -d: -f2)
  BODY=$(echo "$RESP" | grep -v 'HTTP_CODE:' | head -c 150)
  echo "  -> $STATUS: HTTP $HTTP_CODE $BODY"
done

echo ""
echo "=== Step 5: PG — award verification ==="
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "
SELECT 'issuance:', substring(\"OrderId\"::text,1,8), substring(\"CustomerId\"::text,1,8), \"PointsIssued\", \"IssuedAt\"
FROM \"LoyaltyIssuanceRecords\" WHERE \"OrderId\" = '$ORDER_ID';"
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "
SELECT 'pg_rewards:', substring(\"CustomerId\"::text,1,8), substring(\"TenantId\"::text,1,8), \"PointBalance\", \"UpdatedAt\"
FROM \"LoyaltyRewards\" WHERE \"CustomerId\" IN (SELECT \"CustomerId\" FROM \"Orders\" WHERE \"Id\" = '$ORDER_ID');"
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "
SELECT 'pg_customer:', substring(\"Id\"::text,1,8), substring(\"DeviceId\"::text,1,8), \"FullName\" FROM \"Customers\" WHERE \"Id\" IN (SELECT \"CustomerId\" FROM \"Orders\" WHERE \"Id\" = '$ORDER_ID');"

echo ""
echo "=== Step 6: Gateway logs — award + sync event ==="
docker logs vanan-gateway-1 --since 6m 2>&1 | grep -E "LOYALTY: Awarded|LoyaltyBalanceSync|Skipped duplicate|Cannot process loyalty|Customer not found" | tail -10

echo ""
echo "=== OUTPUT ==="
echo "ORDER_ID=$ORDER_ID"
echo "DEVICE=$DEVICE"
echo "TENANT_ID=$TENANT_ID"
