#!/bin/bash
# RV SWEEP — issue #198 (AccountMapRule wire-up + seed) + W6 GĐ1+GĐ2 deploy state
# Run on vanan-gateway (hosts vanan-postgres-1 / VanAnCoreHub). Read-only.
P="docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub"
pass=0; fail=0
ck() { echo "$1"; }

echo "===== [1] EF Migrations — accounting engine waves ====="
$P -tAc "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" ~ '(AccountingEngine|PurchaseInventory|PurchaseReceipt|Stock|ClosingReconcile|CashBank|OrderPayments)' ORDER BY \"MigrationId\";"

echo ""
echo "===== [2] AccountMappingRules — table + row counts ====="
TBL=$($P -tAc "SELECT to_regclass('public.\"AccountMappingRules\"') IS NOT NULL;")
echo "table exists: $TBL"
if [ "$TBL" = "t" ]; then
  $P -c "SELECT coalesce(\"TenantId\"::text,'GLOBAL') AS scope, \"Regime\", \"IsActive\", count(*) FROM \"AccountMappingRules\" GROUP BY 1,2,3 ORDER BY 1,2,3;"
  echo "--- distinct EventCode ---"
  $P -c "SELECT \"EventCode\", count(*) FROM \"AccountMappingRules\" GROUP BY 1 ORDER BY 1;"
else
  echo "FAIL: AccountMappingRules missing"
fi

echo ""
echo "===== [3] TenantAccountCharts — override layer ====="
$P -tAc "SELECT to_regclass('public.\"TenantAccountCharts\"') IS NOT NULL;" 
$P -c "SELECT \"TenantId\", count(*) FROM \"TenantAccountCharts\" GROUP BY 1;" 2>/dev/null || echo "(no rows / table absent)"

echo ""
echo "===== [4] AccountMappingEnabled flag — SystemSettings ====="
$P -c "SELECT \"Key\", \"Value\" FROM \"SystemSettings\" WHERE \"Key\" ILIKE '%AccountMapping%';"

echo ""
echo "===== [5] W6 GĐ1+GĐ2 — expected ABSENT on prod (unpushed) ====="
$P -c "SELECT to_regclass('public.\"PurchaseReceipts\"') AS PurchaseReceipts, to_regclass('public.\"StockMovements\"') AS StockMovements;"
$P -tAc "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" ~ '(PurchaseReceiptWorkflow|StockMovement)' ORDER BY 1;"

echo ""
echo "===== [6] Journals with MappingVersion/rule evidence ====="
$P -c "SELECT \"PostingPurpose\", count(*) FROM \"JournalEntries\" WHERE \"PostingPurpose\" IS NOT NULL GROUP BY 1 ORDER BY 2 DESC LIMIT 15;"

echo ""
echo "===== DONE ====="
