#!/bin/bash
# RV Batch 1 cleanup — xóa tenant test (TaxCode LIKE 'RV%') + restore referral config bị RV deactivate nhầm
# Run on vanan-gateway VPS
set -e
GW=http://gateway:80
TOKEN=$(curl -s -X POST https://app2.khachvip.online/api/platform/login -H "Content-Type: application/json" \
  -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin).get('token',''))")
[ -z "$TOKEN" ] && echo LOGIN_FAILED && exit 1
AUTH="Authorization: Bearer $TOKEN"
PSQL="docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub"

echo "=== 1. Tenant test hiện có (TaxCode LIKE 'RV%') ==="
$PSQL -tAc "SELECT \"Id\" || ' | ' || \"Name\" || ' | ' || \"Status\" FROM \"Tenants\" WHERE \"Settings_TaxCode\" LIKE 'RV%';"

echo ""
echo "=== 2. Restore referral config bị RV deactivate (product 38df680d) ==="
CONFIG=$($PSQL -tAc "SELECT \"Id\" || '|' || \"CommissionRate\" || '|' || \"AppInstallBonus\" || '|' || COALESCE(\"ProductShortCode\",'') || '|' || \"IsActive\" FROM \"ProductReferralConfigs\" WHERE \"ProductId\" = '38df680d-edae-454c-b86f-1dafd2a849c5';")
echo "DB row: $CONFIG"
ID=$(echo "$CONFIG" | cut -d'|' -f1)
RATE=$(echo "$CONFIG" | cut -d'|' -f2)
BONUS=$(echo "$CONFIG" | cut -d'|' -f3)
CODE=$(echo "$CONFIG" | cut -d'|' -f4)
ACTIVE=$(echo "$CONFIG" | cut -d'|' -f5)
echo "→ rate=$RATE bonus=$BONUS code=$CODE active=$ACTIVE"
if [ "$ACTIVE" = "f" ]; then
  echo "--- PUT reactivate với giá trị gốc ---"
  docker exec vanan-nginx-1 curl -s -w "\nHTTP %{http_code}\n" -X PUT $GW/api/admin/products/38df680d-edae-454c-b86f-1dafd2a849c5/referral-config \
    -H "$AUTH" -H "Content-Type: application/json" \
    -d "{\"CommissionRate\":$RATE,\"AppInstallBonus\":$BONUS,\"ProductShortCode\":$(python3 -c "import json,sys;print(json.dumps(sys.argv[1]))" "$CODE"),\"IsActive\":true}"
else
  echo "Config đã active — không cần restore"
fi

echo ""
echo "=== 3. Xóa tenant test + users + groups + crawl sources ==="
TIDS=$($PSQL -tAc "SELECT string_agg('''' || \"Id\"::text || '''', ',') FROM \"Tenants\" WHERE \"Settings_TaxCode\" LIKE 'RV%';")
if [ -n "$TIDS" ]; then
  $PSQL -c "DELETE FROM \"UserPermissionGroups\" WHERE \"PermissionGroupId\" IN (SELECT \"Id\" FROM \"PermissionGroups\" WHERE \"TenantId\" IN ($TIDS));"
  $PSQL -c "DELETE FROM \"PermissionGroups\" WHERE \"TenantId\" IN ($TIDS);"
  $PSQL -c "DELETE FROM \"Users\" WHERE \"TenantId\" IN ($TIDS);"
  $PSQL -c "DELETE FROM \"CrawlSources\" WHERE \"TenantId\" IN ($TIDS);"
  $PSQL -c "DELETE FROM \"Tenants\" WHERE \"Id\" IN ($TIDS);"
  echo "--- sau cleanup ---"
  $PSQL -tAc "SELECT COUNT(*) || ' tenant RV còn lại' FROM \"Tenants\" WHERE \"Settings_TaxCode\" LIKE 'RV%';"
else
  echo "Không có tenant test nào"
fi

echo ""
echo "=== 4. Verify config restored ==="
$PSQL -tAc "SELECT \"Id\" || ' | rate=' || \"CommissionRate\" || ' | active=' || \"IsActive\" FROM \"ProductReferralConfigs\" WHERE \"ProductId\" = '38df680d-edae-454c-b86f-1dafd2a849c5';"
