#!/bin/bash
# Debug #180 round 2: phân biệt diacritics-specific vs no-slug path chung
set -e
GW=http://gateway:80
TOKEN=$(curl -s -X POST https://app2.khachvip.online/api/platform/login -H "Content-Type: application/json" \
  -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin).get('token',''))")
[ -z "$TOKEN" ] && echo LOGIN_FAILED && exit 1
AUTH="Authorization: Bearer $TOKEN"
TS=$(date +%s)

create_and_verify() {
  local NAME="$1" LABEL="$2"
  local TAX="RV${TS}-${LABEL}"
  echo ""
  echo "=== [$LABEL] create: $NAME ==="
  docker exec vanan-nginx-1 curl -s -X POST $GW/api/v1/crawl/batch -H "$AUTH" -H "Content-Type: application/json" \
    -d "[{\"Name\":\"$NAME\",\"TaxCode\":\"$TAX\",\"Address\":\"1 T\",\"IndustryCode\":\"F&B\",\"SourceSite\":\"rv-b\",\"SourceUrl\":\"https://rv/$TS\",\"CrawledAt\":\"2026-09-19T00:00:00Z\"}]"
  echo
  local TID
  TID=$(docker exec vanan-nginx-1 curl -s $GW/api/v1/tenants/pending -H "$AUTH" | python3 -c "
import sys,json
d=json.load(sys.stdin)
m=[t['id'] for t in d if t['name']=='$NAME']
print(m[0] if m else '')")
  echo "[$LABEL] TID=$TID"
  [ -z "$TID" ] && { echo "[$LABEL] NOT FOUND"; return; }
  echo "[$LABEL] verify NO slug:"
  docker exec vanan-nginx-1 curl -s -w " | HTTP %{http_code}\n" -X POST $GW/api/v1/tenants/$TID/verify \
    -H "$AUTH" -H "Content-Type: application/json" \
    -d '{"OwnerUsername":"rvx@vanan.vn","OwnerPassword":"Rv@2026testx","OwnerDisplayName":"RV X","ShopInstanceId":"00000000-0000-0000-0000-000000000001"}'
}

create_and_verify "ABC Cafe ASCII $TS" "ascii"
create_and_verify "Quán Cà Phê Dấu $TS" "dau"

echo ""
echo "=== DONE TS=$TS ==="
