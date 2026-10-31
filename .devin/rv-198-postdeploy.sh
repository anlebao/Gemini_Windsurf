#!/bin/bash
# RV POST-DEPLOY SWEEP — issue #198 (commits 7a13ef99 + cd82dc02)
# Checks: manual-vat-in seeded (proves new ShopERP started + seeder ran) ·
# CongNo bridge wire activation · CashAccountCode · no double-post · routes.
# Run on vanan-gateway (hosts vanan-postgres-1 / VanAnCoreHub). Read-only.
P="docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub"

echo "===== [0] containers on this host ====="
docker ps --format '{{.Names}}' | grep -iE "shoperp|postgres|gateway" | head -6

echo ""
echo "===== [1] manual-vat-in seeded — proves new build + seeder ran ====="
$P -c "SELECT \"Regime\", \"EventCode\", \"DebitAccount\", \"CreditAccount\", \"IsActive\" FROM \"AccountMappingRules\" WHERE \"EventCode\" = 'manual-vat-in' ORDER BY \"Regime\";"

echo ""
echo "===== [2] all global rules by event (expect manual-vat-in=3 accrual regimes) ====="
$P -c "SELECT \"EventCode\", count(*) FROM \"AccountMappingRules\" WHERE \"TenantId\" IS NULL AND \"IsActive\" GROUP BY 1 ORDER BY 1;"

echo ""
echo "===== [3] AccountingEntries.CashAccountCode column ====="
$P -tAc "SELECT column_name FROM information_schema.columns WHERE table_name='AccountingEntries' AND column_name='CashAccountCode';"

echo ""
echo "===== [4] JE purposes — accrual activation check ====="
$P -c "SELECT \"PostingPurpose\", count(*) FROM \"JournalEntries\" GROUP BY 1 ORDER BY 2 DESC LIMIT 15;"

echo ""
echo "===== [5] recent cong-no entries — CashAccountCode set for payments? ====="
$P -c "SELECT \"PostingPurpose\", \"AccountCode\", \"CashAccountCode\", \"PaymentMethod\", \"TransactionDate\" FROM \"AccountingEntries\" WHERE \"EntryType\" = 'Adjustment' AND \"PostingPurpose\" IN ('receivable-payment','payable-payment') ORDER BY \"CreatedAt\" DESC LIMIT 10;"

echo ""
echo "===== [6] EntryPostingStatus — skipped reasons (no-rule observable) ====="
$P -c "SELECT \"Status\", \"SkipReason\", count(*) FROM \"EntryPostingStatuses\" GROUP BY 1,2 ORDER BY 3 DESC LIMIT 10;" 2>/dev/null || \
$P -c "SELECT * FROM \"EntryPostingStatuses\" ORDER BY 1 DESC LIMIT 5;"

echo ""
echo "===== [7] ShopERP DLL markers (if container on this host) ====="
for c in $(docker ps --format '{{.Names}}' | grep -i shoperp); do
  echo "container: $c"
  echo -n "  manual-vat-in in CoreHub.dll: "
  docker exec $c sh -c 'strings /app/VanAn.CoreHub.dll 2>/dev/null | grep -c "manual-vat-in"'
  echo -n "  EntryFormAccountingHelper in ShopERP.dll: "
  docker exec $c sh -c 'strings /app/VanAn.ShopERP.dll 2>/dev/null | grep -c "EntryFormAccountingHelper"'
  echo -n "  CreateInputVatEntryAsync in CoreHub.dll: "
  docker exec $c sh -c 'strings /app/VanAn.CoreHub.dll 2>/dev/null | grep -c "CreateInputVatEntryAsync"'
done
echo ""
echo "===== DONE ====="
