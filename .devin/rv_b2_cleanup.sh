#!/bin/bash
# RV Batch 2 cleanup — xóa tenant test (TaxCode LIKE 'RVB2%') — run on vanan-gateway VPS
set -e
PSQL="docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub"

echo "=== 1. Tenant test RVB2 hiện có ==="
$PSQL -tAc "SELECT \"Id\" || ' | ' || \"Name\" || ' | ' || \"Status\" FROM \"Tenants\" WHERE \"Settings_TaxCode\" LIKE 'RVB2%';"

echo ""
echo "=== 2. Xóa tenant test + users + groups + crawl sources ==="
TIDS=$($PSQL -tAc "SELECT string_agg('''' || \"Id\"::text || '''', ',') FROM \"Tenants\" WHERE \"Settings_TaxCode\" LIKE 'RVB2%';")
if [ -n "$TIDS" ]; then
  $PSQL -c "DELETE FROM \"UserPermissionGroups\" WHERE \"GroupId\" IN (SELECT \"Id\" FROM \"PermissionGroups\" WHERE \"TenantId\" IN ($TIDS));"
  $PSQL -c "DELETE FROM \"PermissionGroups\" WHERE \"TenantId\" IN ($TIDS);"
  $PSQL -c "DELETE FROM \"Users\" WHERE \"TenantId\" IN ($TIDS);"
  $PSQL -c "DELETE FROM \"CrawlSources\" WHERE \"TenantId\" IN ($TIDS);"
  $PSQL -c "DELETE FROM \"Tenants\" WHERE \"Id\" IN ($TIDS);"
  echo "--- sau cleanup ---"
  $PSQL -tAc "SELECT COUNT(*) || ' tenant RVB2 còn lại' FROM \"Tenants\" WHERE \"Settings_TaxCode\" LIKE 'RVB2%';"
else
  echo "Không có tenant test RVB2 nào"
fi
