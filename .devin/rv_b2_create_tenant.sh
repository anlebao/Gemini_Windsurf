#!/bin/bash
# Batch 2 RV (#179): create + verify a test tenant on gateway VPS
set -e
GW=http://gateway:80
TOKEN=$(curl -s -X POST https://app2.khachvip.online/api/platform/login -H "Content-Type: application/json" \
  -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin).get('token',''))")
[ -z "$TOKEN" ] && echo LOGIN_FAILED && exit 1
AUTH="Authorization: Bearer $TOKEN"
TS=$(date +%s)
NAME="RV B2 Cafe $TS"
TAX="RVB2-$TS"

echo "=== create pending: $NAME ==="
docker exec vanan-nginx-1 curl -s -X POST $GW/api/v1/crawl/batch -H "$AUTH" -H "Content-Type: application/json" \
  -d "[{\"Name\":\"$NAME\",\"TaxCode\":\"$TAX\",\"Address\":\"1 RV B2\",\"IndustryCode\":\"F&B\",\"SourceSite\":\"rv-b2\",\"SourceUrl\":\"https://rvb2/$TS\",\"CrawledAt\":\"2026-09-19T00:00:00Z\"}]"
echo

TID=$(docker exec vanan-nginx-1 curl -s $GW/api/v1/tenants/pending -H "$AUTH" | python3 -c "
import sys,json
d=json.load(sys.stdin)
m=[t['id'] for t in d if t['name']=='$NAME']
print(m[0] if m else '')")
echo "TID=$TID"
if [ -z "$TID" ]; then echo "TENANT_NOT_FOUND"; exit 1; fi

echo "=== verify: $TID ==="
docker exec vanan-nginx-1 curl -s -w "\nHTTP %{http_code}\n" -X POST $GW/api/v1/tenants/$TID/verify \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d "{\"OwnerUsername\":\"rvb2o$TS@vanan.vn\",\"OwnerPassword\":\"Rv@2026b2\",\"OwnerDisplayName\":\"RV B2 Owner\",\"ShopInstanceId\":\"00000000-0000-0000-0000-000000000001\"}"

echo ""
echo "=== DONE — TID=$TID TAX=$TAX NAME=$NAME ==="
