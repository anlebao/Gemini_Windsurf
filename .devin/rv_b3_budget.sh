#!/bin/bash
# RV Loyalty Points Integrity BATCH 3 — budget caps API (PUT tenant config + reset counters) + deploy markers
# Run ON vanan-gateway VPS:  gcloud compute ssh vanan-gateway --zone asia-southeast1-a --project vanan-prod --command 'bash -s' < rv_b3_budget.sh
set +e
APP="https://app2.khachvip.online"
API="https://api2.khachvip.online"
PSQL="docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub"
TS=$(date +%s)
TENANT_A="0dfab177-86ad-4513-bd69-ddf956c7b768"   # "Vạn An Test" — active, loyalty ON (RV tenant Batch 2)

echo "############ RV B3 BUDGET — ts=$TS ############"

echo ""
echo "=== L2: Deploy markers (Gateway DLL) ==="
docker exec vanan-gateway-1 sh -c "grep -ac 'MonthlyPointsBudget' /app/VanAn.Gateway.dll" | xargs -I{} echo "  Gateway.dll 'MonthlyPointsBudget': {} (>=1 OK)"
docker exec vanan-gateway-1 sh -c "grep -ac 'reset-counters' /app/VanAn.Gateway.dll" | xargs -I{} echo "  Gateway.dll 'reset-counters': {} (>=1 OK)"

echo ""
echo "=== L1: Login SystemAdmin + snapshot tenant config ==="
TOKEN=$(curl -s -X POST $APP/api/platform/login -H "Content-Type: application/json" \
  -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin).get('token',''))")
[ -z "$TOKEN" ] && echo "LOGIN_FAILED" && exit 1
AUTH="Authorization: Bearer $TOKEN"
echo "  login OK (token ${#TOKEN} chars)"

BEFORE_ROW=$(docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "SELECT count(*) FROM \"LoyaltyTenantConfigs\" WHERE \"TenantId\"='$TENANT_A';")
SNAP=$(curl -s -X GET "$API/api/platform/loyalty/tenant/$TENANT_A/config" -H "$AUTH" -k)
echo "  row existed before: $BEFORE_ROW"
echo "  snapshot: $SNAP"

echo ""
echo "=== L1a: GET default — budget fields present (null = unlimited) ==="
echo "$SNAP" | python3 -c "
import sys,json
d=json.load(sys.stdin)
req=['monthlyPointsBudget','dailyPointsBudget','perCustomerDailyLimit','perOrderRateCap','pointsIssuedThisMonth','pointsIssuedToday']
missing=[f for f in req if f not in d]
print('  missing fields:', missing if missing else 'NONE — all 6 present OK')
"

echo ""
echo "=== L1b: PUT budget caps (monthly=100000, daily=10000, perCustomer=5000, rateCap=5%) ==="
PUT=$(curl -s -X PUT "$API/api/platform/loyalty/tenant/$TENANT_A/config" -H "$AUTH" -H "Content-Type: application/json" -k \
  -d '{"mode":0,"isAllianceMember":false,"maxWalletPoints":null,"monthlyPointsBudget":100000,"dailyPointsBudget":10000,"perCustomerDailyLimit":5000,"perOrderRateCap":0.05}')
echo "  response: $PUT"
echo "$PUT" | python3 -c "
import sys,json
d=json.load(sys.stdin)
ok = d.get('monthlyPointsBudget')==100000 and d.get('dailyPointsBudget')==10000 and d.get('perCustomerDailyLimit')==5000 and abs((d.get('perOrderRateCap') or 0)-0.05)<1e-9
print('  PUT persist check:', 'PASS' if ok else 'FAIL', '(monthly={}, daily={}, perCustomer={}, rateCap={})'.format(d.get('monthlyPointsBudget'),d.get('dailyPointsBudget'),d.get('perCustomerDailyLimit'),d.get('perOrderRateCap')))
"

echo ""
echo "=== L1c: GET lại → budget persisted ==="
GET2=$(curl -s -X GET "$API/api/platform/loyalty/tenant/$TENANT_A/config" -H "$AUTH" -k)
echo "$GET2" | python3 -c "
import sys,json
d=json.load(sys.stdin)
ok = d.get('monthlyPointsBudget')==100000 and d.get('dailyPointsBudget')==10000
print('  GET persist check:', 'PASS' if ok else 'FAIL', '(monthly={}, daily={}, perCustomer={}, rateCap={}, counters=month:{}/today:{})'.format(d.get('monthlyPointsBudget'),d.get('dailyPointsBudget'),d.get('perCustomerDailyLimit'),d.get('perOrderRateCap'),d.get('pointsIssuedThisMonth'),d.get('pointsIssuedToday')))
"

echo ""
echo "=== L1d: Validation — negative budget → 400 ==="
R=$(curl -s -o /dev/null -w '%{http_code}' -X PUT "$API/api/platform/loyalty/tenant/$TENANT_A/config" -H "$AUTH" -H "Content-Type: application/json" -k \
  -d '{"mode":0,"monthlyPointsBudget":-5}')
echo "  PUT monthly=-5 → HTTP $R (expect 400)"

echo ""
echo "=== L1e: Validation — PerOrderRateCap > 1 → 400 ==="
R=$(curl -s -o /dev/null -w '%{http_code}' -X PUT "$API/api/platform/loyalty/tenant/$TENANT_A/config" -H "$AUTH" -H "Content-Type: application/json" -k \
  -d '{"mode":0,"perOrderRateCap":1.5}')
echo "  PUT rateCap=1.5 → HTTP $R (expect 400)"

echo ""
echo "=== L1f: Reset counters daily + monthly (200, counters stay 0) ==="
R=$(curl -s -X POST "$API/api/platform/loyalty/tenant/$TENANT_A/reset-counters" -H "$AUTH" -H "Content-Type: application/json" -k -d '{"scope":"daily"}')
echo "  reset daily → $R"
echo "$R" | python3 -c "
import sys,json
d=json.load(sys.stdin)
print('  daily reset check:', 'PASS' if d.get('pointsIssuedToday')==0 else 'FAIL', '(today={})'.format(d.get('pointsIssuedToday')))
"
R=$(curl -s -X POST "$API/api/platform/loyalty/tenant/$TENANT_A/reset-counters" -H "$AUTH" -H "Content-Type: application/json" -k -d '{"scope":"monthly"}')
echo "  reset monthly → $R"
echo "$R" | python3 -c "
import sys,json
d=json.load(sys.stdin)
print('  monthly reset check:', 'PASS' if d.get('pointsIssuedThisMonth')==0 else 'FAIL', '(month={})'.format(d.get('pointsIssuedThisMonth')))
"

echo ""
echo "=== L1g: Reset counters — invalid scope → 400 ==="
R=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/platform/loyalty/tenant/$TENANT_A/reset-counters" -H "$AUTH" -H "Content-Type: application/json" -k -d '{"scope":"yearly"}')
echo "  reset scope=yearly → HTTP $R (expect 400)"

echo ""
echo "=== CLEANUP: restore snapshot + xóa row nếu trước đó không tồn tại ==="
RESTORE=$(python3 - "$SNAP" "$TOKEN" <<'EOF'
import sys, json, urllib.request, ssl
snap, token = json.loads(sys.argv[1]), sys.argv[2]
body = json.dumps({
  "mode": snap.get("mode"),
  "isAllianceMember": snap.get("isAllianceMember", False),
  "maxWalletPoints": snap.get("maxWalletPoints"),
  "monthlyPointsBudget": snap.get("monthlyPointsBudget"),
  "dailyPointsBudget": snap.get("dailyPointsBudget"),
  "perCustomerDailyLimit": snap.get("perCustomerDailyLimit"),
  "perOrderRateCap": snap.get("perOrderRateCap")
}).encode()
req = urllib.request.Request(f"https://api2.khachvip.online/api/platform/loyalty/tenant/{snap['tenantId']}/config", data=body, method="PUT")
req.add_header("Authorization", f"Bearer {token}")
req.add_header("Content-Type", "application/json")
try:
    with urllib.request.urlopen(req, context=ssl._create_unverified_context()) as r:
        print(r.status)
        sys.exit(0)
except Exception as e:
    print("  restore error:", e)
    sys.exit(1)
EOF
)
echo "  restore PUT → HTTP $RESTORE"
AFTER_ROW=$(docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "SELECT count(*) FROM \"LoyaltyTenantConfigs\" WHERE \"TenantId\"='$TENANT_A';")
if [ "$BEFORE_ROW" = "0" ] && [ "$AFTER_ROW" = "1" ]; then
  docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -c "DELETE FROM \"LoyaltyTenantConfigs\" WHERE \"TenantId\"='$TENANT_A';" > /dev/null
  echo "  row was created by RV (didn't exist before) → DELETED (cleanup)"
else
  echo "  row state unchanged (before=$BEFORE_ROW after=$AFTER_ROW) — no delete needed"
fi

echo "############ RV B3 BUDGET END ############"
