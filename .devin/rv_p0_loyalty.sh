#!/bin/bash
# RV P0 LOYALTY SWEEP — commit 1daf67ee (R1 wallet concurrency / R2 redeem compensation /
# R5 award-only-at-completed + reversal safety net default ON)
# Runs ON vanan-gateway VPS. DO NOT use 'bash -s' stdin (plink mangles it — Pattern #14).
# Deploy: gcloud compute scp .devin/rv_p0_loyalty.sh vanan-gateway:/tmp/rv_p0_loyalty.sh --zone asia-southeast1-a --project vanan-prod
# Run:    gcloud compute ssh vanan-gateway --zone asia-southeast1-a --project vanan-prod --command "bash /tmp/rv_p0_loyalty.sh"
set +e
APP="https://app2.khachvip.online"
API="https://api2.khachvip.online"
PSQL="docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub"
TS=$(date +%s)
TENANT_A="0dfab177-86ad-4513-bd69-ddf956c7b768"   # "Vạn An Test" — designated RV tenant
PRODUCT="00000000-0000-0000-0000-000000000001"
DEVICE="a1${TS: -6}-0000-4000-8000-0000000000a1"
PASS=0; FAIL=0
ok(){ echo "  [PASS] $1"; PASS=$((PASS+1)); }
no(){ echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }
chk(){ [ "$2" = "$3" ] && ok "$1 ($2)" || no "$1 (got=$2 want=$3)"; }

echo "############ RV P0 LOYALTY — ts=$TS device=$DEVICE ############"

echo ""
echo "=== L0: Deploy markers ==="
docker exec vanan-gateway-1 sh -c "grep -ac 'LoyaltyReconcileCase' /app/VanAn.CoreHub.dll" 2>/dev/null | xargs -I{} echo "  CoreHub.dll 'LoyaltyReconcileCase': {} (>=1 OK)"
docker exec vanan-shoperp-1 sh -c "grep -ac 'LoyaltyReconcileCase' /app/VanAn.CoreHub.dll" 2>/dev/null | xargs -I{} echo "  ShopERP CoreHub.dll 'LoyaltyReconcileCase': {} (>=1 OK, absent container = run on shop-a)"

echo ""
echo "=== L1: PG migrations applied ==="
M1=$($PSQL -tAc "SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '20261022102759%';")
M2=$($PSQL -tAc "SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '20261022103507%';")
chk "migration MakeAllianceIdempotencyKeyUnique" "$M1" "1"
chk "migration AddLoyaltyReconcileCase" "$M2" "1"

echo ""
echo "=== L1b: Schema — unique filtered index + reconcile table ==="
IDX=$($PSQL -tAc "SELECT count(*) FROM pg_indexes WHERE tablename='AllianceTransactions' AND indexname='IX_AllianceTransactions_IdempotencyKey' AND indexdef ILIKE '%UNIQUE%';")
chk "unique index IX_...IdempotencyKey" "$IDX" "1"
FILT=$($PSQL -tAc "SELECT count(*) FROM pg_indexes WHERE indexname='IX_AllianceTransactions_IdempotencyKey' AND indexdef ILIKE '%IS NOT NULL%';")
chk "index has IS NOT NULL filter" "$FILT" "1"
TBL=$($PSQL -tAc "SELECT count(*) FROM information_schema.tables WHERE table_name='LoyaltyReconcileCases';")
chk "table LoyaltyReconcileCases (PG)" "$TBL" "1"
DUP=$($PSQL -tAc "SELECT count(*) FROM (SELECT \"IdempotencyKey\" FROM \"AllianceTransactions\" WHERE \"IdempotencyKey\" IS NOT NULL GROUP BY 1 HAVING count(*)>1) d;")
chk "no duplicate idempotency keys post-migration" "$DUP" "0"

echo ""
echo "=== L2: Login + flip tenant A → Alliance (real config API) ==="
COOKIE=/tmp/rv_p0_cookies.txt; rm -f $COOKIE
curl -s -i -c $COOKIE -X POST "$APP/Login" -d 'Username=sysadmin@vanan.vn&Password=2026@vanan&RememberMe=false' -k --max-redirs 0 > /dev/null
TOKEN=$(curl -s -X POST $APP/api/platform/login -H "Content-Type: application/json" \
  -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin).get('token',''))")
[ -z "$TOKEN" ] && echo "LOGIN_FAILED — abort" && exit 1
AUTH="Authorization: Bearer $TOKEN"
echo "  login OK"
CFG_ROWS_BEFORE=$($PSQL -tAc "SELECT count(*) FROM \"LoyaltyTenantConfigs\" WHERE \"TenantId\"='$TENANT_A';")
SNAP=$(curl -s -X GET "$API/api/platform/loyalty/tenant/$TENANT_A/config" -H "$AUTH" -k)
echo "  config rows before: $CFG_ROWS_BEFORE  snapshot: $(echo $SNAP | head -c 200)"
PUT=$(curl -s -X PUT "$API/api/platform/loyalty/tenant/$TENANT_A/config" -H "$AUTH" -H "Content-Type: application/json" -k \
  -d '{"mode":1,"isAllianceMember":true,"maxWalletPoints":null,"monthlyPointsBudget":null,"dailyPointsBudget":null,"perCustomerDailyLimit":null,"perOrderRateCap":null}')
echo "$PUT" | grep -qE '"mode":\s*("Alliance"|1)' \
  && ok "flip tenant A → Alliance mode" || no "flip Alliance mode: $(echo $PUT | head -c 200)"

echo ""
echo "=== L3: R1 — wallet idempotency + conditional guard (internal API) ==="
IKEY=$(docker exec vanan-gateway-1 sh -c 'printf "%s" "$InternalLoyalty__ApiKey"' 2>/dev/null)
[ -z "$IKEY" ] && no "InternalLoyalty__ApiKey unreadable" || echo "  internal key loaded (${#IKEY} chars)"
H="X-Internal-Api-Key: $IKEY"
addp(){ curl -s -X POST "$API/api/internal/loyalty/points/add" -H "$H" -H "Content-Type: application/json" -k \
  -d "{\"customerDeviceId\":\"$DEVICE\",\"tenantId\":\"$TENANT_A\",\"points\":$1,\"reason\":\"$2\",\"idempotencyKey\":\"$3\"}"; }
dedp(){ curl -s -X POST "$API/api/internal/loyalty/points/deduct" -H "$H" -H "Content-Type: application/json" -k \
  -d "{\"customerDeviceId\":\"$DEVICE\",\"tenantId\":\"$TENANT_A\",\"points\":$1,\"reason\":\"$2\",\"voucherCode\":\"RV-$3\",\"idempotencyKey\":\"$3\"}"; }
refp(){ curl -s -X POST "$API/api/internal/loyalty/points/refund" -H "$H" -H "Content-Type: application/json" -k \
  -d "{\"customerDeviceId\":\"$DEVICE\",\"tenantId\":\"$TENANT_A\",\"points\":$1,\"reason\":\"$2\",\"voucherCode\":\"RV-$3\",\"idempotencyKey\":\"$3\"}"; }
bal(){ curl -s -X GET "$API/api/internal/loyalty/wallet/$DEVICE" -H "$H" -k | python3 -c "import sys,json;print(json.load(sys.stdin).get('totalPointBalance','-1'))"; }
txc(){ $PSQL -tAc "SELECT count(*) FROM \"AllianceTransactions\" WHERE \"IdempotencyKey\"='$1';"; }

R=$(addp 100 "rv-p0 add" "rv-add-$TS"); B1=$(bal)
chk "add 100 (K=rv-add-$TS)" "$B1" "100"
R=$(addp 100 "rv-p0 add retry" "rv-add-$TS"); B2=$(bal)
chk "retry same key → balance unchanged" "$B2" "100"
C=$(txc "rv-add-$TS"); chk "tx rows for rv-add key" "$C" "1"

R=$(dedp 30 "rv-p0 spend" "rv-ded-$TS"); B3=$(bal)
chk "deduct 30" "$B3" "70"
R=$(dedp 30 "rv-p0 spend retry" "rv-ded-$TS"); B4=$(bal)
chk "retry same key → balance unchanged" "$B4" "70"
C=$(txc "rv-ded-$TS"); chk "tx rows for rv-ded key" "$C" "1"

R=$(dedp 9999 "rv-p0 overdraft" "rv-ovf-$TS"); B5=$(bal)
echo "$R" | grep -qi '"success":false\|"success": false' && ok "overdraft rejected" || no "overdraft not rejected: $R"
chk "overdraft balance unchanged" "$B5" "70"
C=$(txc "rv-ovf-$TS"); chk "overdraft wrote no ledger row" "$C" "0"

R=$(refp 20 "rv-p0 refund" "rv-ref-$TS"); B6=$(bal)
chk "refund 20" "$B6" "90"
R=$(refp 20 "rv-p0 refund retry" "rv-ref-$TS"); B7=$(bal)
chk "retry same key → balance unchanged" "$B7" "90"

echo ""
echo "=== L3b: R1 — parallel adds, lost-update check (10 x +10 diff keys) ==="
for i in $(seq 1 10); do addp 10 "rv-p0 par$i" "rv-par-$TS-$i" > /tmp/rv_par_$i.json & done; wait
B8=$(bal)
chk "10 parallel +10 → 190" "$B8" "190"
PARROWS=$($PSQL -tAc "SELECT count(*) FROM \"AllianceTransactions\" WHERE \"IdempotencyKey\" LIKE 'rv-par-$TS-%';")
chk "10 parallel tx rows" "$PARROWS" "10"

echo ""
echo "=== L4: R5 — award ONLY at completed (order lifecycle E2E) ==="
IMP=$(curl -s -b $COOKIE -c $COOKIE -X POST $APP/api/admin/impersonate/${TENANT_A} -H "Content-Type: application/json" -k)
TOKEN_A=$(echo "$IMP" | python3 -c "import sys,json;print(json.load(sys.stdin).get('token',''))" 2>/dev/null)
[ -z "$TOKEN_A" ] && no "impersonate A failed" && TOKEN_A="$TOKEN"
ODEV="b1${TS: -6}-0000-4000-8000-0000000000b1"
CO=$(curl -s -X POST $API/api/public/orders/checkout -H "Content-Type: application/json" -k \
  -d "{\"customerDeviceId\":\"$ODEV\",\"customerName\":\"RV P0 Award\",\"customerPhone\":\"0907$TS\",\"customerAddress\":\"RV P0 St\",\"customerNotes\":\"rv-p0-award ts=$TS\",\"orderType\":\"DINEIN\",\"items\":[{\"productId\":\"$PRODUCT\",\"tenantId\":\"$TENANT_A\",\"productName\":\"Cafe Sua Da\",\"quantity\":1,\"unitPrice\":50000,\"vatRate\":0.10}]}")
ORDER=$(echo "$CO" | python3 -c "import sys,json; d=json.load(sys.stdin); o=d.get('orders',[]); print(o[0].get('orderId') if o else '')" 2>/dev/null)
echo "  order: $ORDER"
[ -z "$ORDER" ] && no "checkout failed: $(echo $CO | head -c 200)"
if [ -n "$ORDER" ]; then
  sleep 15
  for STATUS in confirmed preparing ready delivered; do
    R=$(curl -s -o /dev/null -w '%{http_code}' -X PUT "$API/api/orders/${ORDER}/status" -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN_A" -k -d "{\"Status\":\"$STATUS\",\"Reason\":\"rv-p0 ts=$TS\"}")
    echo "  -> $STATUS: HTTP $R"
    sleep 2
  done
  sleep 5
  ISS0=$($PSQL -tAc "SELECT count(*) FROM \"LoyaltyIssuanceRecords\" WHERE \"OrderId\"='$ORDER';")
  chk "issuance after DELIVERED (must be 0 — no delivered award)" "$ISS0" "0"
  AWD0=$(curl -s -X GET "$API/api/internal/loyalty/awarded?orderId=$ORDER&tenantId=$TENANT_A" -H "$H" -k)
  echo "  awarded endpoint after delivered: $AWD0"
  R=$(curl -s -o /dev/null -w '%{http_code}' -X PUT "$API/api/orders/${ORDER}/status" -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN_A" -k -d "{\"Status\":\"completed\",\"Reason\":\"rv-p0 ts=$TS\"}")
  echo "  -> completed: HTTP $R"; sleep 5
  ISS1=$($PSQL -tAc "SELECT count(*) FROM \"LoyaltyIssuanceRecords\" WHERE \"OrderId\"='$ORDER';")
  chk "issuance after COMPLETED (exactly 1)" "$ISS1" "1"
  R=$(curl -s -o /dev/null -w '%{http_code}' -X PUT "$API/api/orders/${ORDER}/status" -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN_A" -k -d "{\"Status\":\"completed\",\"Reason\":\"rv-p0 repeat\"}")
  echo "  -> completed repeat: HTTP $R"; sleep 3
  ISS2=$($PSQL -tAc "SELECT count(*) FROM \"LoyaltyIssuanceRecords\" WHERE \"OrderId\"='$ORDER';")
  chk "repeat completed → still 1 (award-once)" "$ISS2" "1"
  R=$(curl -s -o /dev/null -w '%{http_code}' -X PUT "$API/api/orders/${ORDER}/status" -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN_A" -k -d "{\"Status\":\"cancelled\",\"Reason\":\"rv-p0 terminal-check\"}")
  echo "  -> cancelled after completed: HTTP $R (expect 4xx — completed is terminal in default flow)"
  [ "${R:0:1}" = "4" ] && ok "completed terminal (cancel rejected)" || no "cancel after completed not rejected (HTTP $R) — check tenant flow config"
fi

echo ""
echo "=== L5: R5 — reversal flag effective default ON (no OFF override) ==="
OVR=$($PSQL -tAc "SELECT count(*) FROM \"FeatureFlagOverrides\" f WHERE f.\"FeatureName\"='ValcnV2_RefundReversal' AND f.\"IsEnabled\"=false;" 2>/dev/null)
[ -z "$OVR" ] && OVR="0"
chk "no explicit OFF override for ValcnV2_RefundReversal" "$OVR" "0"

echo ""
echo "=== L6: R2 — admin page /admin/loyalty-reconcile renders ==="
PG=$(curl -s -o /dev/null -w '%{http_code}' -b $COOKIE "$APP/admin/loyalty-reconcile" -k)
echo "  GET /admin/loyalty-reconcile → HTTP $PG"
[ "$PG" = "200" ] && ok "admin page 200" || no "admin page $PG"
BODY=$(curl -s -b $COOKIE "$APP/admin/loyalty-reconcile" -k | head -c 3000)
echo "$BODY" | grep -qi "loyalty\|reconcile" && ok "page marker present" || echo "  (page body marker weak — Blazor SSR may render shell only)"

echo ""
echo "=== CLEANUP: PG test rows + config restore ==="
$PSQL -c "DELETE FROM \"AllianceTransactions\" WHERE \"IdempotencyKey\" LIKE '%$TS%' OR \"VoucherCode\" LIKE '%$TS%';" > /dev/null
$PSQL -c "DELETE FROM \"AllianceWallets\" WHERE \"CustomerDeviceId\"='$DEVICE';" > /dev/null
$PSQL -c "DELETE FROM \"LoyaltyIssuanceRecords\" WHERE \"OrderId\"='$ORDER';" > /dev/null
$PSQL -c "DELETE FROM \"OrderItems\" WHERE \"OrderId\"='$ORDER'; DELETE FROM \"Orders\" WHERE \"Id\"='$ORDER';" > /dev/null
WALLET_LEFT=$($PSQL -tAc "SELECT count(*) FROM \"AllianceWallets\" WHERE \"CustomerDeviceId\"='$DEVICE';")
TX_LEFT=$($PSQL -tAc "SELECT count(*) FROM \"AllianceTransactions\" WHERE \"IdempotencyKey\" LIKE '%$TS%';")
chk "wallet cleaned" "$WALLET_LEFT" "0"
chk "txs cleaned" "$TX_LEFT" "0"
CFG_ROWS_AFTER=$($PSQL -tAc "SELECT count(*) FROM \"LoyaltyTenantConfigs\" WHERE \"TenantId\"='$TENANT_A';")
if [ "$CFG_ROWS_BEFORE" = "0" ] && [ "$CFG_ROWS_AFTER" != "0" ]; then
  $PSQL -c "DELETE FROM \"LoyaltyTenantConfigs\" WHERE \"TenantId\"='$TENANT_A';" > /dev/null
  echo "  config row was created by RV → DELETED"
else
  RCFG=$(curl -s -o /dev/null -w '%{http_code}' -X PUT "$API/api/platform/loyalty/tenant/$TENANT_A/config" -H "$AUTH" -H "Content-Type: application/json" -k -d "$SNAP")
  echo "  config restored via PUT → HTTP $RCFG (before=$CFG_ROWS_BEFORE after=$CFG_ROWS_AFTER)"
fi

echo ""
echo "############ RV P0 LOYALTY END — PASS=$PASS FAIL=$FAIL ############"
[ "$FAIL" = "0" ] && echo "RESULT: ALL GREEN" || echo "RESULT: $FAIL FAILURES — analyze by cluster before fixing"
