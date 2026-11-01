#!/bin/bash
# RV L3 — 4 luồng kế toán "Covered tốt" + double revenue check (2026-11-01)
# Chạy TRÊN VPS vanan-gateway. PG: container vanan-postgres-1, user vanan_admin, db VanAnCoreHub.
# READ-ONLY — chỉ SELECT. Usage: bash /tmp/rv-flows.sh

PGC=vanan-postgres-1; PGU=vanan_admin; DB=VanAnCoreHub
PASS=0; FAIL=0; INFO=0
ck()  { if [ "$2" = "$3" ]; then echo "  PASS $1"; PASS=$((PASS+1)); else echo "  FAIL $1 (got='$2' want='$3')"; FAIL=$((FAIL+1)); fi; }
ckg() { if [ "$2" -ge "$3" ] 2>/dev/null; then echo "  PASS $1 (got=$2 >= $3)"; PASS=$((PASS+1)); else echo "  FAIL $1 (got='$2' < $3)"; FAIL=$((FAIL+1)); fi; }
ckz() { if [ "$2" = "0" ]; then echo "  PASS $1 (0)"; PASS=$((PASS+1)); else echo "  FAIL $1 (found=$2)"; FAIL=$((FAIL+1)); fi; }
info(){ echo "  INFO $1 = $2"; INFO=$((INFO+1)); }
Q() { docker exec "$PGC" psql -U "$PGU" -d "$DB" -tA -c "$1" 2>/dev/null | tr -d '\r'; }

echo "=== A. SCHEMA sanity ==="
for t in JournalEntries JournalEntryLine AccountingEntries VoucherSequences EntryPostingStatuses PurchaseReceipts Orders; do
  ckg "table $t" "$(Q "SELECT COUNT(*) FROM \"$t\";")" 0
done

echo "=== B. PHÂN BỔ JOURNALS ==="
echo "  -- ReferenceType:"; Q 'SELECT "ReferenceType", COUNT(*) FROM "JournalEntries" GROUP BY 1 ORDER BY 2 DESC;' | sed 's/^/      /'
echo "  -- PostingPurpose:"; Q 'SELECT COALESCE("PostingPurpose",'"'"'<null>'"'"'), COUNT(*) FROM "JournalEntries" GROUP BY 1 ORDER BY 2 DESC;' | sed 's/^/      /'
info "CTGS JournalVoucher JEs" "$(Q 'SELECT COUNT(*) FROM "JournalEntries" WHERE "ReferenceType"='"'"'JournalVoucher'"'"';')"
info "Reversal JEs" "$(Q 'SELECT COUNT(*) FROM "JournalEntries" WHERE "IsReversal"=true;')"
info "Vouchers table rows" "$(Q 'SELECT COUNT(*) FROM "Vouchers";' 2>/dev/null)"

echo "=== C. LUỒNG 1 — THU/CHI FORMS ==="
echo "  -- AccountingEntries by EntryType:"; Q 'SELECT "EntryType", COUNT(*) FROM "AccountingEntries" GROUP BY 1 ORDER BY 1;' | sed 's/^/      /'
ckg "entries có VoucherNumber" "$(Q 'SELECT COUNT(*) FROM "AccountingEntries" WHERE "VoucherNumber" IS NOT NULL AND "VoucherNumber"<> '"'"''"'"';')" 1
echo "  -- VoucherSequences:"; Q 'SELECT "VoucherType", COUNT(*) FROM "VoucherSequences" GROUP BY 1 ORDER BY 1;' | sed 's/^/      /'
# Status int: 0=Pending(backfill/pre-bridge) 1=Posted 2=Skipped 3=Failed
ckz "posting Failed (Status=3)" "$(Q 'SELECT COUNT(*) FROM "EntryPostingStatuses" WHERE "Status"=3;')"
info "posting Posted/Skipped/Pending" "$(Q 'SELECT COUNT(*) FILTER (WHERE "Status"=1) || '"'"'/'"'"' || COUNT(*) FILTER (WHERE "Status"=2) || '"'"'/'"'"' || COUNT(*) FILTER (WHERE "Status"=0) FROM "EntryPostingStatuses";')"

echo "=== D. LUỒNG 2 — PHIẾU NHẬP MUA ==="
info "PurchaseReceipts by Status:"; Q 'SELECT "Status", COUNT(*) FROM "PurchaseReceipts" GROUP BY 1 ORDER BY 1;' | sed 's/^/      /'
info "JEs purpose purchase*/purchase-cash" "$(Q 'SELECT COUNT(*) FROM "JournalEntries" WHERE "PostingPurpose" LIKE '"'"'purchase%'"'"';')"
info "purchase reversal JEs (trả hàng)" "$(Q 'SELECT COUNT(*) FROM "JournalEntries" WHERE "IsReversal"=true AND "PostingPurpose" LIKE '"'"'purchase%'"'"';')"

echo "=== E. LUỒNG 3 — ORDER → AUTO JE + DOUBLE REVENUE ==="
info "JEs ReferenceType=Order" "$(Q 'SELECT COUNT(*) FROM "JournalEntries" WHERE "ReferenceType"='"'"'Order'"'"';')"
info "JEs ReferenceType=ManualEntry" "$(Q 'SELECT COUNT(*) FROM "JournalEntries" WHERE "ReferenceType"='"'"'ManualEntry'"'"';')"
# Dup cùng (Tenant,RefType,RefId,Purpose) — chỉ RefId non-null (sự kiện nguồn; NULL = standalone: seed/closing)
ckz "dup JE cùng (Tenant,RefType,RefId,Purpose)" "$(Q 'SELECT COUNT(*) FROM (SELECT 1 FROM "JournalEntries" WHERE "ReferenceId" IS NOT NULL GROUP BY "TenantId","ReferenceType","ReferenceId","PostingPurpose" HAVING COUNT(*)>1) x;')"
# DOUBLE #1: pattern #195 — cùng ReferenceId có cả 'Order' lẫn 'ManualEntry'
ckz "refs có cả JE 'Order' lẫn 'ManualEntry' (double path #195)" "$(Q 'SELECT COUNT(*) FROM (SELECT "ReferenceId" FROM "JournalEntries" WHERE "ReferenceType" IN ('"'"'Order'"'"','"'"'ManualEntry'"'"') GROUP BY "ReferenceId" HAVING COUNT(DISTINCT "ReferenceType")>1) x;')"
# DOUBLE #2: >1 JE revenue purpose per order ref
ckz "orders có >1 JE purpose=revenue" "$(Q 'SELECT COUNT(*) FROM (SELECT "ReferenceId" FROM "JournalEntries" WHERE "PostingPurpose"='"'"'revenue'"'"' AND "ReferenceType"='"'"'Order'"'"' GROUP BY "ReferenceId" HAVING COUNT(*)>1) x;')"
# DOUBLE #3: ΣCó 5xx của JE Order > order TotalAmount
ckz "JE revenue 5xx > order TotalAmount (over-count)" "$(Q 'SELECT COUNT(*) FROM (SELECT j."ReferenceId" FROM "JournalEntries" j JOIN "JournalEntryLine" l ON l."JournalEntryId"=j."Id" JOIN "Orders" o ON o."Id"=j."ReferenceId" WHERE j."ReferenceType"='"'"'Order'"'"' AND l."AccountNumber" LIKE '"'"'5%'"'"' AND j."IsReversal"=false GROUP BY j."ReferenceId", o."TotalAmount" HAVING SUM(l."CreditAmount") > o."TotalAmount" + 0.01) x;')"
# DOUBLE #4: >1 deposit-offset JE per order
ckz "orders có >1 JE deposit-offset" "$(Q 'SELECT COUNT(*) FROM (SELECT "ReferenceId" FROM "JournalEntries" WHERE "PostingPurpose"='"'"'deposit-offset'"'"' GROUP BY "ReferenceId" HAVING COUNT(*)>1) x;')"

echo "=== F. LUỒNG 4 — SỐ DƯ ĐẦU KỲ ==="
info "OpeningBalance JEs" "$(Q 'SELECT COUNT(*) FROM "JournalEntries" WHERE "ReferenceType"='"'"'OpeningBalance'"'"';')"
ckz "OpeningBalance JE không cân đối" "$(Q 'SELECT COUNT(*) FROM (SELECT j."Id" FROM "JournalEntries" j JOIN "JournalEntryLine" l ON l."JournalEntryId"=j."Id" WHERE j."ReferenceType"='"'"'OpeningBalance'"'"' GROUP BY j."Id" HAVING SUM(l."DebitAmount")<>SUM(l."CreditAmount")) x;')"

echo "=== G. CÂN ĐỐI TỔNG ==="
ckz "JE không cân đối (ΣNợ≠ΣCó)" "$(Q 'SELECT COUNT(*) FROM (SELECT j."Id" FROM "JournalEntries" j JOIN "JournalEntryLine" l ON l."JournalEntryId"=j."Id" GROUP BY j."Id" HAVING SUM(l."DebitAmount")<>SUM(l."CreditAmount")) x;')"

echo ""
echo "RESULT: PASS=$PASS FAIL=$FAIL INFO=$INFO"
