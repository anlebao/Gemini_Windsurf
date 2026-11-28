#!/bin/bash
# RV Batch 1 cleanup v2 — restore config 38df680d + xóa tenant test (TaxCode LIKE 'RV%' / 'RVF%')
set -e
GW=http://gateway:80
TOKEN=$(curl -s -X POST https://app2.khachvip.online/api/platform/login -H "Content-Type: application/json" \
  -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin).get('token',''))")
[ -z "$TOKEN" ] && echo LOGIN_FAILED && exit 1
AUTH="Authorization: Bearer $TOKEN"
PSQL="docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub"

echo "=== 1. RESTORE referral config 38df680d (rate=0.03 bonus=10000 code=COMRV1) ==="
docker exec vanan-nginx-1 curl -s -w "\nHTTP %{http_code}\n" -X PUT $GW/api/admin/products/38df680d-edae-454c-b86f-1dafd2a849c5/referral-config \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"CommissionRate":0.03,"AppInstallBonus":10000,"ProductShortCode":"COMRV1","IsActive":true}'
$PSQL -tAc "SELECT 'config now: id=' || \"Id\" || ' active=' || \"IsActive\" || ' rate=' || \"CommissionRate\" FROM \"ProductReferralConfigs\" WHERE \"ProductId\" = '38df680d-edae-454c-b86f-1dafd2a849c5';"

echo ""
echo "=== 2. Xóa tenant test (TaxCode LIKE 'RV%') ==="
TIDS=$($PSQL -tAc "SELECT string_agg('''' || \"Id\"::text || '''', ',') FROM \"Tenants\" WHERE \"Settings_TaxCode\" LIKE 'RV%';")
echo "TIDS=$TIDS"
if [ -n "$TIDS" ]; then
  $PSQL -c "DELETE FROM \"UserPermissionGroups\" WHERE \"GroupId\" IN (SELECT \"Id\" FROM \"PermissionGroups\" WHERE \"TenantId\" IN ($TIDS));" || true
  $PSQL -c "DELETE FROM \"PermissionGroups\" WHERE \"TenantId\" IN ($TIDS);" || true
  $PSQL -c "DELETE FROM \"Users\" WHERE \"TenantId\" IN ($TIDS);" || true
  $PSQL -c "DELETE FROM \"CrawlSources\" WHERE \"TenantId\" IN ($TIDS);" || true
  $PSQL -c "DELETE FROM \"Tenants\" WHERE \"Id\" IN ($TIDS);" || true
fi

echo ""
echo "=== 3. Verify sạch ==="
$PSQL -tAc "SELECT 'tenant RV còn: ' || COUNT(*) FROM \"Tenants\" WHERE \"Settings_TaxCode\" LIKE 'RV%';"
$PSQL -tAc "SELECT 'user RV còn: ' || COUNT(*) FROM \"Users\" WHERE \"Username\" LIKE 'rv%' OR \"Username\" LIKE 'RV%';"
$PSQL -tAc "SELECT 'crawl source RV còn: ' || COUNT(*) FROM \"CrawlSources\" WHERE \"SourceSite\" IN ('rv-batch1','rv-b','rvf');"
