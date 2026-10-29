#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════════
# RV — ACCOUNTING BALANCE & CROSS-VIEW CONSISTENCY (production PG — source of truth)
# Verify số liệu kế toán CÂN BẰNG + ĐÚNG ĐẮN + ĐỒNG BỘ giữa các view:
#   Dashboard tài chính (/financial — API profit-summary, journal-based)
#   Dashboard kế toán     (/accounting — AccountingEntries metrics)
#   Báo cáo tài chính     (IncomeStatement/BalanceSheet/TrialBalance — journals)
#   Sổ HKD                (HKDBookService S1a/S2b/S3a — journals, tenant HKD)
#   Sổ sách               (Nhật ký chung + Sổ cái — GeneralLedgerService journals)
#
# Chạy:  bash rv-scripts/accounting-balance-rv.sh [--period YYYY-MM] [--company <guid>]
#        [--hkd <guid>] [--api] [--gateway-host <ip>] [--zone <z>] [--project <p>]
#   --period : kỳ kiểm tra (mặc định: kỳ mới nhất có journal)
#   --company: tenant CÔNG TY (mặc định auto-detect Type IN (2,3,4) có journal kỳ)
#   --hkd    : tenant HỘ KINH DOANH (mặc định auto-detect Type=1 có journal kỳ)
#   --api    : tầng API — so profit-summary (dashboard tài chính) vs SQL
#              (login sysadmin app2 + impersonate cookie → Bearer → api2)
#
# ⚠️ Chạy TỪ MÁY DEV (cần gcloud auth) — SSH vào vanan-gateway → psql PG.
# ⚠️ READ-ONLY — không ghi/đổi dữ liệu production.
# ═══════════════════════════════════════════════════════════════════════════════
set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# ── args ─────────────────────────────────────────────────────────────────────
PERIOD=""; COMPANY=""; HKD=""; DO_API=0
GATEWAY_HOST="vanan-gateway"; ZONE="asia-southeast1-a"; PROJECT="vanan-prod"
APP2="https://app2.khachvip.online"; API2="https://api2.khachvip.online"
while [ $# -gt 0 ]; do
  case "$1" in
    --period) PERIOD="$2"; shift 2;;
    --company) COMPANY="$2"; shift 2;;
    --hkd) HKD="$2"; shift 2;;
    --api) DO_API=1; shift;;
    --gateway-host) GATEWAY_HOST="$2"; shift 2;;
    --zone) ZONE="$2"; shift 2;;
    --project) PROJECT="$2"; shift 2;;
    --app2) APP2="$2"; shift 2;;
    --api2) API2="$2"; shift 2;;
    *) echo "Unknown arg: $1"; exit 2;;
  esac
done

PASS=0; FAIL=0
chk() { # chk "<name>" <0|1> "<detail>"   (0 = PASS)
  if [ "$2" = "0" ]; then PASS=$((PASS+1)); echo "  [PASS] $1${3:+ — $3}";
  else FAIL=$((FAIL+1)); echo "  [FAIL] $1 — $3"; fi
}
info() { echo "  [INFO] $*"; }

# ── SSH helper (pattern dbcheck.sh — \\\\\" cho quoted column, single-quote concat) ──
GW() { # GW <sql>  → 1 giá trị (psql -t -A). Call sites PHẢI dùng \\\\\"Column\\\\\".
  local out="" i
  for i in 1 2 3; do
    out=$(gcloud compute ssh "$GATEWAY_HOST" --zone "$ZONE" --project "$PROJECT" \
      --command 'docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -t -A -c "'"$1"'"' 2>/dev/null)
    [ -n "$out" ] && break
    sleep 2   # transient SSH reset → retry (mọi query đều COALESCE/count → rỗng = lỗi kết nối)
  done
  echo "$out"
}

# ── 0. Xác định period + tenant ─────────────────────────────────────────────
if [ -z "$PERIOD" ]; then
  PERIOD=$(GW "SELECT to_char(max(\\\"EntryDate\\\"),'YYYY-MM') FROM \\\"JournalEntries\\\";")
fi
YEAR="${PERIOD%-*}"; MONTH="${PERIOD#*-}"
START="$YEAR-$MONTH-01"
NEXT_MONTH=$(date -d "$START +1 month" +%Y-%m-01 2>/dev/null || echo "2099-01-01")

echo "════════════════════════════════════════════════════════════════════"
echo "RV ACCOUNTING BALANCE — period $PERIOD ($START → $NEXT_MONTH) · gateway $GATEWAY_HOST"
echo "════════════════════════════════════════════════════════════════════"

if [ -z "$COMPANY" ]; then
  COMPANY=$(GW "SELECT \\\"Id\\\"::text FROM \\\"Tenants\\\" t WHERE t.\\\"Type\\\" IN (2,3,4) AND EXISTS (SELECT 1 FROM \\\"JournalEntries\\\" j WHERE j.\\\"TenantId\\\"=t.\\\"Id\\\" AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date) ORDER BY t.\\\"Name\\\" LIMIT 1;")
fi
if [ -z "$HKD" ]; then
  HKD=$(GW "SELECT \\\"Id\\\"::text FROM \\\"Tenants\\\" t WHERE t.\\\"Type\\\"=1 AND EXISTS (SELECT 1 FROM \\\"JournalEntries\\\" j WHERE j.\\\"TenantId\\\"=t.\\\"Id\\\" AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date) ORDER BY t.\\\"Name\\\" LIMIT 1;")
fi
info "Company tenant: ${COMPANY:-<none>} | HKD tenant: ${HKD:-<none>}"
[ -n "$COMPANY" ] && info "  company name: $(GW "SELECT \\\"Name\\\" FROM \\\"Tenants\\\" WHERE \\\"Id\\\"='$COMPANY'::uuid;")"
[ -n "$HKD" ] && info "  hkd name: $(GW "SELECT \\\"Name\\\" FROM \\\"Tenants\\\" WHERE \\\"Id\\\"='$HKD'::uuid;")"

# helper: chạy 1 check cho cả 2 tenant (nếu có)
for_each_tenant() { # for_each_tenant <func>  — func nhận $T (guid) + $TAG
  for T in "$COMPANY" "$HKD"; do
    [ -z "$T" ] && continue
    TAG=$( [ "$T" = "$COMPANY" ] && echo company || echo hkd )
    "$1" "$T" "$TAG"
  done
}

# ── 1. C1: Journal cân bằng (Σdebit == Σcredit per journal) ────────────────
echo ""
echo "── C1. Journal balance (per journal Σdebit == Σcredit) ──"
c1() {
  local T="$1" TAG="$2"
  local C1
  C1=$(GW "SELECT count(*) FROM (SELECT j.\\\"Id\\\" FROM \\\"JournalEntries\\\" j JOIN \\\"JournalEntryLine\\\" l ON l.\\\"JournalEntryId\\\"=j.\\\"Id\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date GROUP BY j.\\\"Id\\\" HAVING ABS(SUM(l.\\\"DebitAmount\\\")-SUM(l.\\\"CreditAmount\\\")) > 0.01) x;")
  chk "C1[$TAG] journal lệch Nợ/Có = ${C1:-ERR} (expect 0)" "$( [ "${C1:-1}" = "0" ] && echo 0 || echo 1 )" "unbalanced=${C1:-}"
}
for_each_tenant c1

# ── 2. C2: Trial balance kỳ (Σdebit == Σcredit) ─────────────────────────────
echo ""
echo "── C2. Trial balance (period) — Σdebit == Σcredit ──"
c2() {
  local T="$1" TAG="$2" C2 DB CR DIFF
  C2=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\"),0)||'|'||COALESCE(SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date;")
  DB=$(echo "$C2" | cut -d'|' -f1); CR=$(echo "$C2" | cut -d'|' -f2)
  DIFF=$(awk "BEGIN{d=$DB-$CR; print (d<0?-d:d)}")
  chk "C2[$TAG] ΣNợ=$DB ΣCó=$CR diff=$DIFF" "$(awk "BEGIN{print ($DIFF<=0.01)?0:1}")" "diff=$DIFF"
}
for_each_tenant c2

# ── 3. C3: Doanh thu/Chi phí — đồng bộ Journal ↔ AccountingEntries ─────────
echo ""
echo "── C3. Revenue/Expense consistency (journals ↔ AccountingEntries ↔ UI metrics) ──"
c3() {
  local T="$1" TAG="$2"
  local RV_J EXP_J RV_E VAT_E EXP_E OFF_E D_REV D_EXP C3D
  # Journal: revenue = credit 5xx/7xx · expense = debit 6xx/8xx (kỳ)
  RV_J=$(GW "SELECT COALESCE(SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date AND l.\\\"AccountNumber\\\" ~ '^[57]';")
  EXP_J=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date AND l.\\\"AccountNumber\\\" ~ '^[68]';")
  # AccountingEntries (TransactionDate kỳ): Revenue (EntryType=1) / Expense (2) — subset account giống journal
  RV_E=$(GW "SELECT COALESCE(SUM(\\\"Amount\\\"),0) FROM \\\"AccountingEntries\\\" WHERE \\\"TenantId\\\"='$T'::uuid AND \\\"EntryType\\\"=1 AND \\\"AccountCode\\\" ~ '^[57]' AND \\\"TransactionDate\\\" >= '$START'::date AND \\\"TransactionDate\\\" < '$NEXT_MONTH'::date;")
  VAT_E=$(GW "SELECT COALESCE(SUM(\\\"Amount\\\"),0) FROM \\\"AccountingEntries\\\" WHERE \\\"TenantId\\\"='$T'::uuid AND \\\"EntryType\\\"=1 AND \\\"AccountCode\\\"='3331' AND \\\"TransactionDate\\\" >= '$START'::date AND \\\"TransactionDate\\\" < '$NEXT_MONTH'::date;")
  EXP_E=$(GW "SELECT COALESCE(SUM(\\\"Amount\\\"),0) FROM \\\"AccountingEntries\\\" WHERE \\\"TenantId\\\"='$T'::uuid AND \\\"EntryType\\\"=2 AND \\\"AccountCode\\\" ~ '^[68]' AND \\\"TransactionDate\\\" >= '$START'::date AND \\\"TransactionDate\\\" < '$NEXT_MONTH'::date;")
  # Offset cọc (DepositOffset EntryType=5, accountCode 5/7): journal credit 511 của offset KHÔNG có ở
  # entries Revenue — journal revenue = entries revenue + offset (cọc chuyển thành doanh thu).
  OFF_E=$(GW "SELECT COALESCE(SUM(\\\"Amount\\\"),0) FROM \\\"AccountingEntries\\\" WHERE \\\"TenantId\\\"='$T'::uuid AND \\\"EntryType\\\"=5 AND \\\"AccountCode\\\" ~ '^[57]' AND \\\"TransactionDate\\\" >= '$START'::date AND \\\"TransactionDate\\\" < '$NEXT_MONTH'::date;")
  D_REV=$(awk "BEGIN{d=$RV_J-($RV_E+$OFF_E); print (d<0?-d:d)}")
  D_EXP=$(awk "BEGIN{d=$EXP_J-$EXP_E; print (d<0?-d:d)}")
  info "[$TAG] Revenue: journal 5xx/7xx credit=$RV_J · entries Revenue(5/7, trừ VAT)=$RV_E · offset cọc(5/7)=$OFF_E · VAT 3331 entries=$VAT_E"
  info "[$TAG] Expense: journal 6xx/8xx debit=$EXP_J · entries Expense(6/8)=$EXP_E"
  chk "C3a[$TAG] Revenue journal == entries(5/7)+offset(5/7) diff=$D_REV" "$(awk "BEGIN{print ($D_REV<=0.01)?0:1}")" "revJ=$RV_J revE=$RV_E off=$OFF_E"
  chk "C3b[$TAG] Expense journal == entries (6/8) diff=$D_EXP" "$(awk "BEGIN{print ($D_EXP<=0.01)?0:1}")" "expJ=$EXP_J expE=$EXP_E"
  # C3d diagnostic: journal ref 'Order' toàn lịch sử (pre-fix double-revenue) — giải thích C3a lệch nếu data cũ
  C3D=$(GW "SELECT count(*) FROM \\\"JournalEntries\\\" WHERE \\\"TenantId\\\"='$T'::uuid AND \\\"ReferenceType\\\"='Order';")
  [ "${C3D:-0}" != "0" ] && info "[$TAG] ⚠️ còn ${C3D} journal ref 'Order' (pre-fix double-revenue) — C3a lệch DO data cũ, cần cleanup như order 01a18615/01a185f0"
  # Dashboard kế toán (AccountingIndex) hiển thị MonthlyRevenue = entries Revenue GỒM VAT — in để đối chiếu UI
  info "[$TAG] AccountingIndex MonthlyRevenue (UI, gồm VAT 3331) = $(awk "BEGIN{print $RV_E+$VAT_E}") · NetProfit(entries) = $(awk "BEGIN{print $RV_E+$VAT_E-$EXP_E}")"
}
for_each_tenant c3

# ── 4. C4: Balance identity (tài sản = nợ + vốn + lãi) — toàn bộ lịch sử ───
echo ""
echo "── C4. Balance identity (all periods — Tài sản = Nợ + Vốn + Lãi) ──"
c4() {
  local T="$1" TAG="$2"
  local A L E P X NET RHS DIFF
  A=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\")-SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\" ~ '^[12]';")
  L=$(GW "SELECT COALESCE(SUM(l.\\\"CreditAmount\\\")-SUM(l.\\\"DebitAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\" ~ '^3';")
  E=$(GW "SELECT COALESCE(SUM(l.\\\"CreditAmount\\\")-SUM(l.\\\"DebitAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\" ~ '^4';")
  P=$(GW "SELECT COALESCE(SUM(l.\\\"CreditAmount\\\")-SUM(l.\\\"DebitAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\" ~ '^[57]';")
  X=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\")-SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\" ~ '^[68]';")
  NET=$(awk "BEGIN{print $P-$X}")
  RHS=$(awk "BEGIN{print $L+$E+$NET}")
  DIFF=$(awk "BEGIN{d=$A-$RHS; print (d<0?-d:d)}")
  info "[$TAG] Tài sản(1xx+2xx)=$A · Nợ(3xx)=$L · Vốn(4xx)=$E · Lãi(5/7−6/8)=$NET → Nợ+Vốn+Lãi=$RHS"
  # Lưu ý: TK đối ứng 214/229 (contra) normal-credit — nếu lệch nhỏ, kiểm tra tay.
  chk "C4[$TAG] Tài sản == Nợ+Vốn+Lãi diff=$DIFF" "$(awk "BEGIN{print ($DIFF<=0.01)?0:1}")" "asset=$A rhs=$RHS"
}
for_each_tenant c4

# ── 5. C5: Sổ cái (ledger) — opening + movement + closing per account ──────
echo ""
echo "── C5. Sổ cái (ledger) — key accounts: opening(before $PERIOD) → movement → closing ──"
LEDGER_ACCOUNTS="111 112 131 156 331 3387 421 511 632 3331"
c5() {
  local T="$1" TAG="$2" AC OP MV CL
  echo "  [$TAG] account | opening | movement | closing"
  for AC in $LEDGER_ACCOUNTS; do
    OP=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\")-SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\"='$AC' AND j.\\\"EntryDate\\\" < '$START'::date;")
    MV=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\")-SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\"='$AC' AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date;")
    CL=$(awk "BEGIN{print $OP+$MV}")
    echo "    $AC | $OP | $MV | $CL"
  done
}
for_each_tenant c5

# ── 6. C6: Nhật ký chung (journal report — period) ─────────────────────────
echo ""
echo "── C6. Nhật ký chung (journal report — period) ──"
c6() {
  local T="$1" TAG="$2" C6 CNT DB CR DIFF
  C6=$(GW "SELECT count(*)||'|'||COALESCE(SUM(l.\\\"DebitAmount\\\"),0)||'|'||COALESCE(SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date;")
  CNT=$(echo "$C6" | cut -d'|' -f1); DB=$(echo "$C6" | cut -d'|' -f2); CR=$(echo "$C6" | cut -d'|' -f3)
  DIFF=$(awk "BEGIN{d=$DB-$CR; print (d<0?-d:d)}")
  chk "C6[$TAG] journal entries=$CNT · ΣNợ=$DB · ΣCó=$CR diff=$DIFF" "$(awk "BEGIN{print ($DIFF<=0.01)?0:1}")" "count=$CNT"
}
for_each_tenant c6

# ── 7. C7: Sổ HKD (tenant HKD — books S1a/S2b/S3a = toàn bộ journal kỳ) ────
echo ""
echo "── C7. Sổ HKD (HKD tenant — books = journal set kỳ, không filter book type) ──"
if [ -n "$HKD" ]; then
  C7=$(GW "SELECT count(*)||'|'||COALESCE(SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$HKD'::uuid AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date AND l.\\\"AccountNumber\\\" ~ '^[57]';")
  CNT=$(echo "$C7" | cut -d'|' -f1); REV=$(echo "$C7" | cut -d'|' -f2)
  info "[hkd] S1a/S2b/S3a book = $CNT journals · revenue 5xx/7xx credit = $REV (khớp C3a journal revenue)"
  chk "C7[hkd] HKD book có journal (count=$CNT > 0)" "$( [ "$CNT" -gt 0 ] 2>/dev/null && echo 0 || echo 1 )" "count=$CNT"
else
  info "Không có HKD tenant — skip C7"
fi

# ── 8. C8: Single-source bridge (double-revenue regression) ─────────────────
echo ""
echo "── C8. Single-source bridge (double-revenue regression) ──"
c8() {
  local T="$1" TAG="$2" C8 C8B
  C8=$(GW "SELECT count(*) FROM \\\"JournalEntries\\\" WHERE \\\"TenantId\\\"='$T'::uuid AND \\\"ReferenceType\\\"='Order' AND \\\"EntryDate\\\" >= '2026-10-29'::date;")
  chk "C8[$TAG] journal ref 'Order' sau ngày fix (2026-10-29) = ${C8:-ERR} (expect 0)" "$( [ "${C8:-1}" = "0" ] && echo 0 || echo 1 )" "count=${C8:-}"
  # Journal per paid order (kỳ): expect 2 (revenue+COGS) hoặc 3 (kèm offset deposit 3387)
  C8B=$(GW "SELECT o.\\\"Id\\\"::text, count(DISTINCT j.\\\"Id\\\") FROM \\\"Orders\\\" o JOIN \\\"AccountingEntries\\\" e ON e.\\\"Reference\\\" = o.\\\"Id\\\"::text OR e.\\\"Reference\\\" = o.\\\"Id\\\"::text || '-DEPOSIT-OFFSET' JOIN \\\"JournalEntries\\\" j ON j.\\\"ReferenceId\\\"=e.\\\"Id\\\" WHERE o.\\\"TenantId\\\"='$T'::uuid AND o.\\\"PaymentStatus\\\"='Paid' AND o.\\\"CreatedAt\\\" >= '$START'::timestamp GROUP BY o.\\\"Id\\\" HAVING count(DISTINCT j.\\\"Id\\\") NOT IN (2,3) ORDER BY o.\\\"Id\\\";")
  if [ -z "$C8B" ]; then chk "C8b[$TAG] mọi order paid kỳ có đúng 1 bộ journal (2-3)" 0 "ok"; else chk "C8b[$TAG] order có số journal lệch (expect 2/3)" 1 "$(echo "$C8B" | head -5)"; fi
}
for_each_tenant c8

# ── 9. C9: Deposit 3387 residual (cọc đã thu − offset đã trừ) ───────────────
echo ""
echo "── C9. Deposit 3387 residual (cọc đã thu − offset đã trừ, toàn bộ lịch sử) ──"
c9() {
  local T="$1" TAG="$2" C9
  C9=$(GW "SELECT COALESCE(SUM(l.\\\"CreditAmount\\\")-SUM(l.\\\"DebitAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\"='3387';")
  info "[$TAG] 3387 residual = $C9 (0 = mọi cọc đã đối trừ; >0 = cọc đã thu chưa thanh toán cuối)"
}
for_each_tenant c9

# ── 10. C10 (--api): Dashboard tài chính profit-summary vs journal SQL ──────
if [ "$DO_API" = "1" ] && [ -n "$COMPANY" ]; then
  echo ""
  echo "── C10. Dashboard tài chính (API profit-summary) vs journal SQL ──"
  JAR=$(mktemp /tmp/rv-acct-jar-XXXX)
  curl -s -c "$JAR" -X POST "$APP2/api/platform/login" -H 'Content-Type: application/json' \
    -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' >/dev/null
  IMP=$(curl -s -b "$JAR" -c "$JAR" -X POST "$APP2/api/admin/impersonate/$COMPANY")
  TOK=$(echo "$IMP" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('token') or '')" 2>/dev/null)
  if [ -z "$TOK" ]; then
    chk "C10 impersonate company" 1 "no token (cookie auth)"
  else
    PS=$(curl -s -H "Authorization: Bearer $TOK" "$API2/api/financial/profit-summary?period=$PERIOD")
    PSRV=$(echo "$PS" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('revenue') or d.get('totalRevenue') or '')" 2>/dev/null)
    RV_J=$(GW "SELECT COALESCE(SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$COMPANY'::uuid AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date AND l.\\\"AccountNumber\\\" ~ '^[57]';")
    info "[company] API profit-summary revenue=$PSRV · journal revenue=$RV_J"
    DIFF=$(awk "BEGIN{d=$PSRV-$RV_J; print (d<0?-d:d)}")
    chk "C10 profit-summary == journal revenue diff=$DIFF" "$(awk "BEGIN{print ($DIFF<=0.01)?0:1}")" "api=$PSRV sql=$RV_J"
  fi
  rm -f "$JAR"
elif [ "$DO_API" = "1" ]; then
  chk "C10 --api cần --company" 1 "no company tenant"
fi

echo ""
echo "════════════════════════════════════════════════════════════════════"
echo "RV ACCOUNTING SUMMARY: $PASS PASS · $FAIL FAIL"
echo "════════════════════════════════════════════════════════════════════"
[ "$FAIL" = "0" ]