#!/bin/bash
# RV Batch 1 — issues #180 (verify tenant 400) + #178 (referral config rate=0) + #177 (L1 referral scan shape)
# Run on vanan-gateway VPS: gcloud compute ssh vanan-gateway --zone asia-southeast1-a --project vanan-prod
# Login: POST /api/platform/login trên ShopERP (mint JWT). API: docker exec nginx -> gateway:80 (team pattern).

set -e
SHOPERP_BASE=https://app2.khachvip.online
GW=http://gateway:80
TS=$(date +%s)
echo "=== RV Batch 1 (issues 176-181) — ts=$TS ==="

# ── 0. Login SystemAdmin ────────────────────────────────────────────────
TOKEN=$(curl -s -X POST $SHOPERP_BASE/api/platform/login -H "Content-Type: application/json" \
  -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin).get('token',''))" 2>/dev/null)
if [ -z "$TOKEN" ]; then echo "LOGIN_FAILED"; exit 1; fi
echo "[OK] Login token len=${#TOKEN}"
AUTH="Authorization: Bearer $TOKEN"

# ── #180 — verify Pending tenant tên tiếng Việt (was 400) ───────────────
NAME="Quán Cà Phê RV $TS"
TAX="RV-$TS"
echo ""
echo "=== #180: tạo Pending tenant test '$NAME' ==="
docker exec vanan-nginx-1 curl -s -X POST $GW/api/v1/crawl/batch -H "$AUTH" -H "Content-Type: application/json" \
  -d "[{\"Name\":\"$NAME\",\"TaxCode\":\"$TAX\",\"Address\":\"123 RV Test St\",\"IndustryCode\":\"F&B\",\"SourceSite\":\"rv-batch1\",\"SourceUrl\":\"https://rv.test/$TS\",\"CrawledAt\":\"2026-09-19T00:00:00Z\"}]" | head -c 300
echo

TID=$(docker exec vanan-nginx-1 curl -s $GW/api/v1/tenants/pending -H "$AUTH" | python3 -c "
import sys,json
d=json.load(sys.stdin)
print([t['id'] for t in d if t['name']=='$NAME'][0])" 2>/dev/null)
echo "[INFO] Pending tenant id=$TID"

SID=$(docker exec vanan-nginx-1 curl -s $GW/api/v1/shop-instances -H "$AUTH" | python3 -c "
import sys,json
d=json.load(sys.stdin)
print(d[0]['id'])" 2>/dev/null)
echo "[INFO] ShopInstance id=$SID"

echo "--- POST /api/v1/tenants/$TID/verify (expect HTTP 200 + slug chuẩn hóa) ---"
docker exec vanan-nginx-1 curl -s -w "\nHTTP %{http_code}\n" -X POST $GW/api/v1/tenants/$TID/verify \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d "{\"OwnerUsername\":\"rvowner$TS@vanan.vn\",\"OwnerPassword\":\"Rv@2026test$TS\",\"OwnerDisplayName\":\"RV Owner $TS\",\"ShopInstanceId\":\"$SID\"}"

echo ""
echo "--- GET tenant sau verify — kiểm tra slug ---"
docker exec vanan-nginx-1 curl -s $GW/api/v1/tenants/$TID -H "$AUTH" | python3 -c "
import sys,json
d=json.load(sys.stdin)
print('slug =', d.get('slug') or '(settings)', '| status =', d.get('status'))" 2>/dev/null || true

# ── #178 — referral config CommissionRate=0 (was 400) ───────────────────
echo ""
echo "=== #178: tạo referral-config rate=0 (expect 201) ==="
CAT=$(docker exec vanan-nginx-1 curl -s "$GW/api/catalog/recommended?page=1&pageSize=1")
PID=$(echo "$CAT" | python3 -c "import sys,json;print(json.load(sys.stdin)['products'][0]['productId'])" 2>/dev/null || echo "")
TEN=$(echo "$CAT" | python3 -c "import sys,json;print(json.load(sys.stdin)['products'][0]['tenantId'])" 2>/dev/null || echo "")
echo "[INFO] featured product=$PID tenant=$TEN"
if [ -n "$PID" ]; then
  docker exec vanan-nginx-1 curl -s -w "\nHTTP %{http_code}\n" -X POST $GW/api/admin/products/$PID/referral-config \
    -H "$AUTH" -H "Content-Type: application/json" \
    -d "{\"TenantId\":\"$TEN\",\"CommissionRate\":0,\"AppInstallBonus\":0,\"ProductShortCode\":\"RVZERO$TS\"}"
  echo "--- cleanup: DELETE config ---"
  docker exec vanan-nginx-1 curl -s -o /dev/null -w "DELETE HTTP %{http_code}\n" -X DELETE $GW/api/admin/products/$PID/referral-config -H "$AUTH"
fi

# ── #177 (L1, best-effort) — referral scan DTO shape ────────────────────
echo ""
echo "=== #177 (L1): GET /api/community/referral/{code} shape (nếu có code) ==="
CODE="${1:-}"
if [ -n "$CODE" ]; then
  ENC=$(python3 -c "import urllib.parse,sys;print(urllib.parse.quote(sys.argv[1]))" "$CODE")
  docker exec vanan-nginx-1 curl -s -w "\nHTTP %{http_code}\n" "$GW/api/community/referral/$ENC" | head -c 500
else
  echo "[SKIP] không có composite code — test qua UI (L3)"
fi

echo ""
echo "=== DONE — cleanup tenant + user + groups qua SQL ==="
echo "TID=$TID TAX=$TAX"
