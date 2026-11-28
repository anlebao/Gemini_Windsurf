#!/bin/bash
# RV Loyalty Points Integrity BATCH 2 — PG ledger single source of truth + POS proxy + tenant attribution
# Run ON vanan-gateway VPS:  gcloud compute ssh vanan-gateway ... --command 'bash -s' < rv_b2_ledger.sh
set +e
APP="https://app2.khachvip.online"
API="https://api2.khachvip.online"
PSQL="docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub"
TS=$(date +%s)
TENANT_A="0dfab177-86ad-4513-bd69-ddf956c7b768"   # "Vạn An Test" — active, loyalty ON (default)
TENANT_B="b391c72b-7d31-4644-880d-324d89a16db6"   # "Central Mall" — active (2nd tenant cho attribution)
PRODUCT="00000000-0000-0000-0000-000000000001"

echo "############ RV B2 LEDGER — ts=$TS ############"

echo ""
echo "=== L1: Deploy markers ==="
docker exec vanan-gateway-1 sh -c "grep -ac 'LoyaltyPointLedger' /app/VanAn.CoreHub.dll" | xargs -I{} echo "  Gateway CoreHub.dll marker (LoyaltyPointLedger): {} (>=2 OK)"

echo ""
echo "=== L2: BACKFILL loyalty-backfill-pg (ShopERP admin) ==="
COOKIE=/tmp/rv_b2_cookies.txt
rm -f $COOKIE
curl -s -i -c $COOKIE -X POST "$APP/Login" -d 'Username=sysadmin@vanan.vn&Password=2026@vanan&RememberMe=false' -k --max-redirs 0 > /dev/null
TOKEN=$(curl -s -X POST $APP/api/platform/login -H "Content-Type: application/json" \
  -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin).get('token',''))")
[ -z "$TOKEN" ] && echo "LOGIN_FAILED" && exit 1
AUTH="Authorization: Bearer $TOKEN"
echo "  login OK (token ${#TOKEN} chars)"
BF=$(curl -s -X POST $APP/api/admin/sync/loyalty-backfill-pg -H "$AUTH" -H "Content-Type: application/json")
echo "  backfill response: $BF"

echo ""
echo "=== L3: PG state sau backfill — LoyaltyRewards per tenant ==="
$PSQL -tAc "
SELECT substring(\"TenantId\"::text,1,8) AS tenant, count(*) AS rows, sum(\"PointBalance\") AS total_points
FROM \"LoyaltyRewards\" GROUP BY \"TenantId\" ORDER BY 2 DESC LIMIT 10;"

echo ""
echo "=== L4: E2E-A — checkout tenant A (deviceA) → complete qua GATEWAY (in-proc ledger) ==="
IMP=$(curl -s -b $COOKIE -c $COOKIE -X POST $APP/api/admin/impersonate/${TENANT_A} -H "Content-Type: application/json" -k)
TOKEN_A=$(echo "$IMP" | python3 -c "import sys,json;print(json.load(sys.stdin).get('token',''))" 2>/dev/null)
[ -z "$TOKEN_A" ] && echo "IMPERSONATE_A_FAILED" && exit 1
DEVICE_A="b2a$(date +%s)00-0000-4000-8000-000000000001"
CO=$(curl -s -X POST $API/api/public/orders/checkout -H "Content-Type: application/json" -k \
  -d "{\"customerDeviceId\":\"$DEVICE_A\",\"customerName\":\"RV B2 Ledger A\",\"customerPhone\":\"0909$TS\",\"customerAddress\":\"RV B2 1 St\",\"customerNotes\":\"rv-b2-ledger ts=$TS\",\"orderType\":\"DINEIN\",\"items\":[{\"productId\":\"$PRODUCT\",\"tenantId\":\"$TENANT_A\",\"productName\":\"Cafe Sua Da\",\"quantity\":2,\"unitPrice\":25000,\"vatRate\":0.10}]}")
ORDER_A=$(echo "$CO" | python3 -c "import sys,json; d=json.load(sys.stdin); o=d.get('orders',[]); print(o[0].get('orderId') if o else '')" 2>/dev/null)
echo "  checkout tenant A → order $ORDER_A"
[ -z "$ORDER_A" ] && echo "CHECKOUT_A_FAILED: $CO" && exit 1
sleep 15
for STATUS in confirmed preparing ready delivered completed; do
  R=$(curl -s -o /dev/null -w '%{http_code}' -X PUT "$API/api/orders/${ORDER_A}/status" -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN_A" -k -d "{\"Status\":\"$STATUS\",\"Reason\":\"rv-b2-ledger ts=$TS\"}")
  echo "  gateway transition -> $STATUS: HTTP $R"
done
$PSQL -tAc "
SELECT '  issuance:', substring(\"OrderId\"::text,1,8), substring(\"CustomerId\"::text,1,8), \"PointsIssued\" FROM \"LoyaltyIssuanceRecords\" WHERE \"OrderId\" = '$ORDER_A';"
$PSQL -tAc "
SELECT '  pg_row:', substring(\"CustomerId\"::text,1,8), substring(\"TenantId\"::text,1,8), \"PointBalance\" FROM \"LoyaltyRewards\" lr WHERE EXISTS (SELECT 1 FROM \"Orders\" o WHERE o.\"Id\"='$ORDER_A' AND o.\"CustomerId\"=lr.\"CustomerId\") ORDER BY lr.\"TenantId\";"
echo "  → guard re-run (delivered→completed again):"
R=$(curl -s -X PUT "$API/api/orders/${ORDER_A}/status" -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN_A" -k -d "{\"Status\":\"completed\",\"Reason\":\"rv-b2-guard ts=$TS\"}")
echo "  $R" | head -c 300; echo ""

echo ""
echo "=== L5: E2E-B — checkout tenant A (deviceB) → complete qua SHOPERP app2 (HTTP proxy → Gateway /award) ==="
DEVICE_B="b2b$(date +%s)00-0000-4000-8000-000000000002"
CO2=$(curl -s -X POST $API/api/public/orders/checkout -H "Content-Type: application/json" -k \
  -d "{\"customerDeviceId\":\"$DEVICE_B\",\"customerName\":\"RV B2 Ledger B\",\"customerPhone\":\"0908$TS\",\"customerAddress\":\"RV B2 2 St\",\"customerNotes\":\"rv-b2-proxy ts=$TS\",\"orderType\":\"DINEIN\",\"items\":[{\"productId\":\"$PRODUCT\",\"tenantId\":\"$TENANT_A\",\"productName\":\"Cafe Sua Da\",\"quantity\":1,\"unitPrice\":25000,\"vatRate\":0.10}]}")
ORDER_B=$(echo "$CO2" | python3 -c "import sys,json; d=json.load(sys.stdin); o=d.get('orders',[]); print(o[0].get('orderId') if o else '')" 2>/dev/null)
echo "  checkout tenant A → order $ORDER_B"
[ -z "$ORDER_B" ] && echo "CHECKOUT_B_FAILED" && exit 1
sleep 15
for STATUS in confirmed preparing ready delivered completed; do
  R=$(curl -s -o /dev/null -w '%{http_code}' -X PUT "$APP/api/orderworkflow/${ORDER_B}/status" -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN_A" -k -d "{\"Status\":\"$STATUS\",\"Reason\":\"rv-b2-proxy ts=$TS\"}")
  echo "  shoperp transition -> $STATUS: HTTP $R"
done
$PSQL -tAc "
SELECT '  issuance:', substring(\"OrderId\"::text,1,8), substring(\"CustomerId\"::text,1,8), \"PointsIssued\" FROM \"LoyaltyIssuanceRecords\" WHERE \"OrderId\" = '$ORDER_B';"

echo ""
echo "=== L6: E2E-C — tenant attribution: cùng deviceB earn thêm tại tenant B (Central Mall) ==="
CO3=$(curl -s -X POST $API/api/public/orders/checkout -H "Content-Type: application/json" -k \
  -d "{\"customerDeviceId\":\"$DEVICE_B\",\"customerName\":\"RV B2 Ledger B\",\"customerPhone\":\"0908$TS\",\"customerAddress\":\"RV B2 2 St\",\"customerNotes\":\"rv-b2-attr ts=$TS\",\"orderType\":\"DINEIN\",\"items\":[{\"productId\":\"$PRODUCT\",\"tenantId\":\"$TENANT_B\",\"productName\":\"Cafe Sua Da\",\"quantity\":2,\"unitPrice\":25000,\"vatRate\":0.10}]}")
ORDER_C=$(echo "$CO3" | python3 -c "import sys,json; d=json.load(sys.stdin); o=d.get('orders',[]); print(o[0].get('orderId') if o else '')" 2>/dev/null)
echo "  checkout tenant B → order $ORDER_C"
[ -z "$ORDER_C" ] && echo "CHECKOUT_C_FAILED" && exit 1
sleep 15
IMP_B=$(curl -s -b $COOKIE -c $COOKIE -X POST $APP/api/admin/impersonate/${TENANT_B} -H "Content-Type: application/json" -k)
TOKEN_B=$(echo "$IMP_B" | python3 -c "import sys,json;print(json.load(sys.stdin).get('token',''))" 2>/dev/null)
for STATUS in confirmed preparing ready delivered completed; do
  R=$(curl -s -o /dev/null -w '%{http_code}' -X PUT "$API/api/orders/${ORDER_C}/status" -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN_B" -k -d "{\"Status\":\"$STATUS\",\"Reason\":\"rv-b2-attr ts=$TS\"}")
  echo "  gateway transition tenant B -> $STATUS: HTTP $R"
done
CUST_B=$(docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "SELECT \"CustomerId\" FROM \"Orders\" WHERE \"Id\"='$ORDER_C';")
echo "  customer: $CUST_B"
$PSQL -tAc "
SELECT '  pg_rows:', substring(\"TenantId\"::text,1,8), \"PointBalance\" FROM \"LoyaltyRewards\" WHERE \"CustomerId\" = '$CUST_B' ORDER BY \"TenantId\";"
echo "  (kỳ vọng 2 row riêng — tenant A + tenant B — KHÔNG merge)"

echo ""
echo "=== L7: mirror SQLite shop-a (LoyaltySyncSubscriber) ==="
echo "  (chạy riêng trên vanan-shop-a: rv_b2_mirror.sh)"
echo "############ RV B2 LEDGER END ############"
