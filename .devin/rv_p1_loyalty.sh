#!/bin/bash
# RV P1 LOYALTY SWEEP — commit 4c8186d4 (P1.3 admin ADJUST · P1.1 member-exit settle-then-burn
# · P1.2 cross-tenant settlement report — audit Q3/Q4/Q6)
# Runs ON vanan-gateway VPS. DO NOT use 'bash -s' stdin (plink mangles it — Pattern #14).
# Deploy: gcloud compute scp .devin/rv_p1_loyalty.sh vanan-gateway:/tmp/rv_p1_loyalty.sh --zone asia-southeast1-a --project vanan-prod
# Run:    gcloud compute ssh vanan-gateway --zone asia-southeast1-a --project vanan-prod --command "bash /tmp/rv_p1_loyalty.sh"
set +e
APP="https://app2.khachvip.online"
API="https://api2.khachvip.online"
PSQL="docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub"
TS=$(date +%s)
FROM_TS=$(date -u +%Y-%m-%dT%H:%M:%SZ)
TENANT_A="0dfab177-86ad-4513-bd69-ddf956c7b768"   # "Vạn An Test" — designated RV tenant
TENANT_B="bb0000${TS: -2}-0000-4000-8000-0000000000b2"  # FAKE tenant — no config row → never a member (safe exit target)
DEVICE1="a1${TS: -6}-0000-4000-8000-0000000000a1"
DEVICE2="b2${TS: -6}-0000-4000-8000-0000000000b2"
PASS=0; FAIL=0
ok(){ echo "  [PASS] $1"; PASS=$((PASS+1)); }
no(){ echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }
chk(){ [ "$2" = "$3" ] && ok "$1 ($2)" || no "$1 (got=$2 want=$3)"; }

echo "############ RV P1 LOYALTY — ts=$TS ############"

echo ""
echo "=== L0: Deploy markers ==="
docker exec vanan-gateway-1 sh -c "grep -ac 'ExitMemberSettlementAsync' /app/VanAn.CoreHub.dll" 2>/dev/null | xargs -I{} echo "  Gateway CoreHub.dll 'ExitMemberSettlementAsync': {} (>=1 OK)"
docker exec vanan-gateway-1 sh -c "grep -ac 'settlement-report' /app/VanAn.Gateway.dll" 2>/dev/null | xargs -I{} echo "  Gateway.dll 'settlement-report' route: {} (>=1 OK)"
docker exec vanan-shoperp-1 sh -c "grep -ac 'member-exit' /app/VanAn.ShopERP.dll" 2>/dev/null | xargs -I{} echo "  ShopERP.dll 'member-exit': {} (>=1 OK — absent container = run on shop-a)"

echo ""
echo "=== L1: Login SystemAdmin + flip tenant A → Alliance member ==="
COOKIE=/tmp/rv_p1_cookies.txt; rm -f $COOKIE
curl -s -i -c $COOKIE -X POST "$APP/Login" -d 'Username=sysadmin@vanan.vn&Password=2026@vanan&RememberMe=false' -k --max-redirs 0 > /dev/null
TOKEN=$(curl -s -X POST $APP/api/platform/login -H "Content-Type: application/json" \
  -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin).get('token',''))")
[ -z "$TOKEN" ] && echo "LOGIN_FAILED — abort" && exit 1
AUTH="Authorization: Bearer $TOKEN"
echo "  login OK"
CFG_ROWS_BEFORE=$($PSQL -tAc "SELECT count(*) FROM \"LoyaltyTenantConfigs\" WHERE \"TenantId\"='$TENANT_A';")
SNAP=$(curl -s -X GET "$API/api/platform/loyalty/tenant/$TENANT_A/config" -H "$AUTH" -k)
echo "  config rows before: $CFG_ROWS_BEFORE  snapshot: $(echo $SNAP | head -c 160)"
PUT=$(curl -s -X PUT "$API/api/platform/loyalty/tenant/$TENANT_A/config" -H "$AUTH" -H "Content-Type: application/json" -k \
  -d '{"mode":1,"isAllianceMember":true,"maxWalletPoints":null,"monthlyPointsBudget":null,"dailyPointsBudget":null,"perCustomerDailyLimit":null,"perOrderRateCap":null}')
echo "$PUT" | grep -qE '"isAllianceMember":\s*true' \
  && ok "tenant A → alliance member" || no "flip member: $(echo $PUT | head -c 200)"

echo ""
echo "=== L2: P1.3 — POST /api/internal/loyalty/points/adjust ==="
IKEY=$(docker exec vanan-gateway-1 sh -c 'printf "%s" "$InternalLoyalty__ApiKey"' 2>/dev/null)
[ -z "$IKEY" ] && no "InternalLoyalty__ApiKey unreadable" || echo "  internal key loaded (${#IKEY} chars)"
H="X-Internal-Api-Key: $IKEY"
addp(){ curl -s -X POST "$API/api/internal/loyalty/points/add" -H "$H" -H "Content-Type: application/json" -k \
  -d "{\"customerDeviceId\":\"$1\",\"tenantId\":\"$2\",\"points\":$3,\"reason\":\"$4\",\"idempotencyKey\":\"$5\"}"; }
dedp(){ curl -s -X POST "$API/api/internal/loyalty/points/deduct" -H "$H" -H "Content-Type: application/json" -k \
  -d "{\"customerDeviceId\":\"$1\",\"tenantId\":\"$2\",\"points\":$3,\"reason\":\"$4\",\"voucherCode\":\"RV-$5\",\"idempotencyKey\":\"$5\"}"; }
adjp(){ curl -s -X POST "$API/api/internal/loyalty/points/adjust" -H "$H" -H "Content-Type: application/json" -k -d "$1"; }
adjcode(){ curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/internal/loyalty/points/adjust" -H "$H" -H "Content-Type: application/json" -k -d "$1"; }
bal(){ curl -s -X GET "$API/api/internal/loyalty/wallet/$1" -H "$H" -k | python3 -c "import sys,json;print(json.load(sys.stdin).get('totalPointBalance','-1'))"; }
txsum(){ $PSQL -tAc "SELECT COALESCE(sum(\"Points\"),0) FROM \"AllianceTransactions\" WHERE \"IdempotencyKey\"='$1';"; }
txtype(){ $PSQL -tAc "SELECT \"Type\" FROM \"AllianceTransactions\" WHERE \"IdempotencyKey\"='$1' LIMIT 1;"; }

# seed DEVICE1 at tenant A
R=$(addp "$DEVICE1" "$TENANT_A" 100 "rv-p1 seed" "rv-p1-add-$TS"); B=$(bal $DEVICE1)
chk "seed DEVICE1 +100" "$B" "100"

# positive adjust
R=$(adjp "{\"customerDeviceId\":\"$DEVICE1\",\"tenantId\":\"$TENANT_A\",\"points\":40,\"reason\":\"rv-p1 adjust+\",\"idempotencyKey\":\"rv-p1-adjp-$TS\"}")
echo "$R" | grep -q '"success":true' && ok "adjust +40 success" || no "adjust +40: $R"
T=$(txtype "rv-p1-adjp-$TS"); chk "tx type is ADJUST (2)" "$T" "2"
B=$(bal $DEVICE1); chk "balance after +40" "$B" "140"

# retry same key → dedup
R=$(adjp "{\"customerDeviceId\":\"$DEVICE1\",\"tenantId\":\"$TENANT_A\",\"points\":40,\"reason\":\"rv-p1 adjust+\",\"idempotencyKey\":\"rv-p1-adjp-$TS\"}")
B=$(bal $DEVICE1); chk "adjust retry same key → still 140" "$B" "140"
C=$($PSQL -tAc "SELECT count(*) FROM \"AllianceTransactions\" WHERE \"IdempotencyKey\"='rv-p1-adjp-$TS';")
chk "single ADJUST row for key" "$C" "1"

# negative adjust
R=$(adjp "{\"customerDeviceId\":\"$DEVICE1\",\"tenantId\":\"$TENANT_A\",\"points\":-15,\"reason\":\"rv-p1 adjust-\",\"idempotencyKey\":\"rv-p1-adjm-$TS\"}")
echo "$R" | grep -q '"success":true' && ok "adjust -15 success" || no "adjust -15: $R"
S=$(txsum "rv-p1-adjm-$TS"); chk "ADJUST row stores -15" "$S" "-15"
B=$(bal $DEVICE1); chk "balance after -15" "$B" "125"

# overdraft negative adjust → rejected, no row
R=$(adjp "{\"customerDeviceId\":\"$DEVICE1\",\"tenantId\":\"$TENANT_A\",\"points\":-9999,\"reason\":\"rv-p1 ovf\",\"idempotencyKey\":\"rv-p1-ovf-$TS\"}")
echo "$R" | grep -qi '"success":false\|"success": false' && ok "adjust -9999 rejected" || no "overdraft adjust not rejected: $R"
B=$(bal $DEVICE1); chk "balance unchanged 125" "$B" "125"
C=$($PSQL -tAc "SELECT count(*) FROM \"AllianceTransactions\" WHERE \"IdempotencyKey\"='rv-p1-ovf-$TS';")
chk "overdraft wrote no row" "$C" "0"

# points=0 → 400
C=$(adjcode "{\"customerDeviceId\":\"$DEVICE1\",\"tenantId\":\"$TENANT_A\",\"points\":0,\"reason\":\"rv-p1 zero\",\"idempotencyKey\":\"rv-p1-zero-$TS\"}")
chk "points=0 → HTTP 400" "$C" "400"

# missing reason → 400 (audit)
C=$(adjcode "{\"customerDeviceId\":\"$DEVICE1\",\"tenantId\":\"$TENANT_A\",\"points\":10,\"reason\":\"\",\"idempotencyKey\":\"rv-p1-noreason-$TS\"}")
chk "empty reason → HTTP 400" "$C" "400"
B=$(bal $DEVICE1); chk "balance still 125 after bad requests" "$B" "125"

echo ""
echo "=== L3: P1.1 — member-exit: guard (still member) then settle-then-burn (opted-out) ==="
# DEVICE2: 100 @A + 50 @B + redeem 30 @B → net {A:100, B:20}, balance 120
R=$(addp "$DEVICE2" "$TENANT_A" 100 "rv-p1 d2 seedA" "rv-p1-d2a-$TS")
R=$(addp "$DEVICE2" "$TENANT_B" 50 "rv-p1 d2 seedB" "rv-p1-d2b-$TS")
R=$(dedp "$DEVICE2" "$TENANT_B" 30 "rv-p1 d2 spendB" "rv-p1-d2r-$TS")
B=$(bal $DEVICE2); chk "DEVICE2 balance 120 (100A+50B-30B)" "$B" "120"

# exit TENANT_A while still member → must be rejected (guard)
R=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/platform/loyalty/member-exit" -H "$AUTH" -H "Content-Type: application/json" -k \
  -d "{\"tenantId\":\"$TENANT_A\",\"settlementNote\":\"rv-p1 guard check ts=$TS\"}")
chk "member-exit on still-member → HTTP 400" "$R" "400"
B=$(bal $DEVICE2); chk "no burn — balance still 120" "$B" "120"

# exit TENANT_B (fake tenant, no config row → not member) → burn its 20 attributed
R=$(curl -s -X POST "$API/api/platform/loyalty/member-exit" -H "$AUTH" -H "Content-Type: application/json" -k \
  -d "{\"tenantId\":\"$TENANT_B\",\"settlementNote\":\"rv-p1 exit settle ts=$TS\"}")
echo "  member-exit response: $(echo $R | head -c 200)"
CP=$(echo "$R" | python3 -c "import sys,json;print(json.load(sys.stdin).get('customersProcessed',-1))" 2>/dev/null)
TP=$(echo "$R" | python3 -c "import sys,json;print(json.load(sys.stdin).get('totalPointsTransferred',-1))" 2>/dev/null)
chk "exit B: 1 wallet burned" "$CP" "1"
chk "exit B: 20 points burned" "$TP" "20"
B=$(bal $DEVICE2); chk "balance 120-20 → 100" "$B" "100"
WID=$($PSQL -tAc "SELECT \"Id\" FROM \"AllianceWallets\" WHERE \"CustomerDeviceId\"='$DEVICE2';")
EK="exit:$TENANT_B:$WID"
S=$(txsum "$EK"); chk "ADJUST row -20 with exit key" "$S" "-20"

# re-run exit B → idempotent, 0 processed
R=$(curl -s -X POST "$API/api/platform/loyalty/member-exit" -H "$AUTH" -H "Content-Type: application/json" -k \
  -d "{\"tenantId\":\"$TENANT_B\",\"settlementNote\":\"rv-p1 exit rerun ts=$TS\"}")
CP=$(echo "$R" | python3 -c "import sys,json;print(json.load(sys.stdin).get('customersProcessed',-1))" 2>/dev/null)
chk "re-run exit B → 0 processed (idempotent)" "$CP" "0"
B=$(bal $DEVICE2); chk "balance still 100" "$B" "100"
C=$($PSQL -tAc "SELECT count(*) FROM \"AllianceTransactions\" WHERE \"IdempotencyKey\"='$EK';")
chk "single exit burn row" "$C" "1"

# member-exit with empty note → 400
R=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/platform/loyalty/member-exit" -H "$AUTH" -H "Content-Type: application/json" -k \
  -d "{\"tenantId\":\"$TENANT_B\",\"settlementNote\":\"\"}")
chk "member-exit empty note → HTTP 400" "$R" "400"

echo ""
echo "=== L4: P1.2 — GET settlement-report (math check, date-filtered to this run) ==="
RPT=$(curl -s -X GET "$API/api/platform/loyalty/settlement-report?from=$FROM_TS" -H "$AUTH" -k)
echo "$RPT" | python3 -c "
import sys, json
rows = json.load(sys.stdin)
ta, tb = '$TENANT_A', '$TENANT_B'
ra = next((r for r in rows if r['tenantId'] == ta), None)
rb = next((r for r in rows if r['tenantId'] == tb), None)
# Expected since FROM_TS:  A: EARN 100(d1)+100(d2)=200, REDEEM 0, ADJUST +40-15=+25 → NET 225
#                          B: EARN 50, REDEEM 30, ADJUST -20(exit burn) → NET 0
print('ROWA|' + ('missing' if not ra else '%s|%s|%s|%s' % (ra['earnCredit'], ra['redeemCost'], ra['adjustNet'], ra['netSettlement'])))
print('ROWB|' + ('missing' if not rb else '%s|%s|%s|%s' % (rb['earnCredit'], rb['redeemCost'], rb['adjustNet'], rb['netSettlement'])))
" > /tmp/rv_p1_rpt.txt
cat /tmp/rv_p1_rpt.txt
ROWA=$(grep ROWA /tmp/rv_p1_rpt.txt | cut -d'|' -f2-)
ROWB=$(grep ROWB /tmp/rv_p1_rpt.txt | cut -d'|' -f2-)
chk "report A: earn=200 redeem=0 adjust=+25 net=+225" "$ROWA" "200|0|25|225"
chk "report B: earn=50 redeem=30 adjust=-20 net=0" "$ROWB" "50|30|-20|0"

# date range excludes future
RPT2=$(curl -s -X GET "$API/api/platform/loyalty/settlement-report?from=2099-01-01T00:00:00Z" -H "$AUTH" -k)
EMPTY=$(echo "$RPT2" | python3 -c "import sys,json;print(len(json.load(sys.stdin)))" 2>/dev/null)
chk "report from=2099 → 0 rows (date filter works)" "$EMPTY" "0"

echo ""
echo "=== L5: admin page /admin/loyalty-reconcile renders (SystemAdmin sections) ==="
PG=$(curl -s -o /dev/null -w '%{http_code}' -b $COOKIE "$APP/admin/loyalty-reconcile" -k)
chk "admin page HTTP 200" "$PG" "200"

echo ""
echo "=== CLEANUP: test rows + config restore ==="
$PSQL -c "DELETE FROM \"AllianceTransactions\" WHERE \"WalletId\" IN (SELECT \"Id\" FROM \"AllianceWallets\" WHERE \"CustomerDeviceId\" IN ('$DEVICE1','$DEVICE2'));" > /dev/null
$PSQL -c "DELETE FROM \"AllianceWallets\" WHERE \"CustomerDeviceId\" IN ('$DEVICE1','$DEVICE2');" > /dev/null
W_LEFT=$($PSQL -tAc "SELECT count(*) FROM \"AllianceWallets\" WHERE \"CustomerDeviceId\" IN ('$DEVICE1','$DEVICE2');")
T_LEFT=$($PSQL -tAc "SELECT count(*) FROM \"AllianceTransactions\" WHERE \"IdempotencyKey\" LIKE '%$TS%' OR \"IdempotencyKey\" LIKE 'exit:$TENANT_B%';")
chk "wallets cleaned" "$W_LEFT" "0"
chk "txs cleaned" "$T_LEFT" "0"
CFG_ROWS_AFTER=$($PSQL -tAc "SELECT count(*) FROM \"LoyaltyTenantConfigs\" WHERE \"TenantId\"='$TENANT_A';")
if [ "$CFG_ROWS_BEFORE" = "0" ] && [ "$CFG_ROWS_AFTER" != "0" ]; then
  $PSQL -c "DELETE FROM \"LoyaltyTenantConfigs\" WHERE \"TenantId\"='$TENANT_A';" > /dev/null
  echo "  config row created by RV → DELETED"
else
  RCFG=$(curl -s -o /dev/null -w '%{http_code}' -X PUT "$API/api/platform/loyalty/tenant/$TENANT_A/config" -H "$AUTH" -H "Content-Type: application/json" -k -d "$SNAP")
  echo "  config restored via PUT → HTTP $RCFG"
fi

echo ""
echo "############ RV P1 LOYALTY END — PASS=$PASS FAIL=$FAIL ############"
[ "$FAIL" = "0" ] && echo "RESULT: ALL GREEN" || echo "RESULT: $FAIL FAILURES — analyze by cluster before fixing"
