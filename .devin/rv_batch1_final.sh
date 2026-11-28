#!/bin/bash
# RV Batch 1 FINAL — sau fix #180 v2 (explicit Vietnamese map, invariant-globalization safe)
set -e
GW=http://gateway:80
TOKEN=$(curl -s -X POST https://app2.khachvip.online/api/platform/login -H "Content-Type: application/json" \
  -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin).get('token',''))")
[ -z "$TOKEN" ] && echo LOGIN_FAILED && exit 1
AUTH="Authorization: Bearer $TOKEN"
TS=$(date +%s)

echo "=== RV FINAL — ts=$TS ==="

# ── #180: verify tenant tên tiếng Việt KHÔNG slug (kỳ vọng 200) ──────────
NAME="Quán Cà Phê Đậm Vị $TS"
TAX="RVF-$TS"
echo ""
echo "=== [#180] create: $NAME ==="
docker exec vanan-nginx-1 curl -s -X POST $GW/api/v1/crawl/batch -H "$AUTH" -H "Content-Type: application/json" \
  -d "[{\"Name\":\"$NAME\",\"TaxCode\":\"$TAX\",\"Address\":\"1 F\",\"IndustryCode\":\"F&B\",\"SourceSite\":\"rvf\",\"SourceUrl\":\"https://rvf/$TS\",\"CrawledAt\":\"2026-09-19T00:00:00Z\"}]"
echo
TID=$(docker exec vanan-nginx-1 curl -s $GW/api/v1/tenants/pending -H "$AUTH" | python3 -c "
import sys,json
d=json.load(sys.stdin)
m=[t['id'] for t in d if t['name']=='$NAME']
print(m[0] if m else '')")
echo "[#180] TID=$TID"
echo "--- verify NO slug (kỳ vọng 200 + slug 'quan-ca-phe-dam-vi-...') ---"
docker exec vanan-nginx-1 curl -s -w "\nHTTP %{http_code}\n" -X POST $GW/api/v1/tenants/$TID/verify \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"OwnerUsername":"rvfinal@vanan.vn","OwnerPassword":"Rv@2026final","OwnerDisplayName":"RV Final","ShopInstanceId":"00000000-0000-0000-0000-000000000001"}'

# ── #178: referral config rate=0 trên product CHƯA có config (kỳ vọng 201) ──
echo ""
echo "=== [#178] referral-config rate=0 — tìm product chưa có config ==="
PID=""; TEN=""; PIDX=0
while [ -z "$PID" ] && [ $PIDX -lt 8 ]; do
  ITEM=$(docker exec vanan-nginx-1 curl -s "$GW/api/catalog/recommended?page=1&pageSize=8" | python3 -c "
import sys,json
d=json.load(sys.stdin)
p=d['products'][$PIDX]
print(p['productId'] + ' ' + str(p['tenantId']))" 2>/dev/null || echo "")
  CAND_PID=$(echo "$ITEM" | cut -d' ' -f1)
  CAND_TEN=$(echo "$ITEM" | cut -d' ' -f2)
  EXISTS=$(docker exec vanan-nginx-1 curl -s -o /dev/null -w "%{http_code}" $GW/api/admin/products/$CAND_PID/referral-config -H "$AUTH")
  if [ "$EXISTS" = "404" ]; then PID=$CAND_PID; TEN=$CAND_TEN; echo "[#178] chọn product chưa có config: $PID (tenant $TEN)"; fi
  PIDX=$((PIDX+1))
done
if [ -n "$PID" ]; then
  echo "--- POST rate=0 (kỳ vọng 201) ---"
  docker exec vanan-nginx-1 curl -s -w "\nHTTP %{http_code}\n" -X POST $GW/api/admin/products/$PID/referral-config \
    -H "$AUTH" -H "Content-Type: application/json" \
    -d "{\"TenantId\":\"$TEN\",\"CommissionRate\":0,\"AppInstallBonus\":0,\"ProductShortCode\":\"RVFZ$TS\"}"
  echo "--- cleanup: DELETE ---"
  docker exec vanan-nginx-1 curl -s -o /dev/null -w "DELETE HTTP %{http_code}\n" -X DELETE $GW/api/admin/products/$PID/referral-config -H "$AUTH"
else
  echo "[#178] KHÔNG tìm thấy product chưa có config trong 8 sản phẩm đầu — skip"
fi

echo ""
echo "=== DONE — TID=$TID TAX=$TAX ==="
