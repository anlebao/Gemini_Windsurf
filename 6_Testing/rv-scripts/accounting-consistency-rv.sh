#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════════
# RV — ACCOUNTING CROSS-SURFACE CONSISTENCY SWEEP (production)
# Mục tiêu: số liệu ĐÚNG + NHẤT QUÁN giữa mọi bề mặt người dùng nhìn thấy:
#   Báo cáo tài chính VAS : /api/trial-balances · /api/balance-sheets (B01)
#                           /api/income-statements (B02) · /api/cash-flow-statements (B03)
#   Sổ sách               : Sổ Tổng hợp (TB) · Nhật ký chung · Sổ cái (key accounts)
#   Báo cáo quản trị      : api2 /api/financial/profit-summary (dashboard tài chính)
#   Dashboard kế toán     : AccountingIndex metrics (MonthlyRevenue/Expense/NetProfit — entries)
#   Công nợ               : entries 131/331 vs journal 131/331 (CongNo service basis)
#   Toàn cục              : per-JE balance · per-tenant sum == global · JE không line
#
# Kiến trúc check:
#   - SQL ground truth : gcloud ssh vanan-gateway → docker exec vanan-postgres-1 psql
#   - API truth        : app2 VAS endpoints (Bearer JWT từ impersonate sysadmin)
#   - Reports đọc TẤT CẢ JE (service không filter closing/reversal) → SQL dùng cùng dataset.
#
# Chạy:  bash rv-scripts/accounting-consistency-rv.sh [--period YYYY-MM] [--tenant guid]* 
#        [--max-tenants N] [--no-api] [--gateway-host h] [--zone z] [--project p]
#   --period      : kỳ kiểm tra (mặc định: kỳ mới nhất có journal)
#   --tenant      : thêm tenant cụ thể (lặp được). Mặc định: mọi tenant có JE kỳ (cap N)
#   --max-tenants : giới hạn số tenant auto-detect (default 6)
#   --no-api      : chỉ SQL ground truth (không cần login/impersonate)
#
# ⚠️ Chạy TỪ MÁY DEV (cần gcloud auth + curl + python3). READ-ONLY.
# ═══════════════════════════════════════════════════════════════════════════════
set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPORT="$SCRIPT_DIR/accounting-consistency-report.txt"
: > "$REPORT"

PERIOD=""; TENANTS=(); MAXT=6; DO_API=1
GATEWAY_HOST="vanan-gateway"; ZONE="asia-southeast1-a"; PROJECT="vanan-prod"
APP2="https://app2.khachvip.online"; API2="https://api2.khachvip.online"
while [ $# -gt 0 ]; do
  case "$1" in
    --period) PERIOD="$2"; shift 2;;
    --tenant) TENANTS+=("$2"); shift 2;;
    --max-tenants) MAXT="$2"; shift 2;;
    --no-api) DO_API=0; shift;;
    --gateway-host) GATEWAY_HOST="$2"; shift 2;;
    --zone) ZONE="$2"; shift 2;;
    --project) PROJECT="$2"; shift 2;;
    --app2) APP2="$2"; shift 2;;
    --api2) API2="$2"; shift 2;;
    *) echo "Unknown arg: $1"; exit 2;;
  esac
done

PASS=0; FAIL=0; WARN=0
out() { echo "$*" | tee -a "$REPORT"; }
chk() { # chk "<name>" <0|1> "<detail>"   (0 = PASS)
  if [ "$2" = "0" ]; then PASS=$((PASS+1)); out "  [PASS] $1${3:+ — $3}";
  else FAIL=$((FAIL+1)); out "  [FAIL] $1 — $3"; fi
}
info() { out "  [INFO] $*"; }
warn() { WARN=$((WARN+1)); out "  [WARN] $*"; }

# ── SSH helper (pattern accounting-balance-rv.sh — \\\\\" cho quoted column) ──
GW() { # GW <sql> → psql -t -A trên vanan-postgres-1 (retry ×3 — transient SSH reset)
  local out="" i
  for i in 1 2 3; do
    out=$(gcloud compute ssh "$GATEWAY_HOST" --zone "$ZONE" --project "$PROJECT" \
      --command 'docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -t -A -c "'"$1"'"' 2>/dev/null)
    [ -n "$out" ] && break
    sleep 2
  done
  echo "$out"
}

jget() { # jget <json> <path> — python3 tolerant; '' khi thiếu/rỗng/error
  echo "$1" | python3 -c "import sys,json
try:
    d=json.load(sys.stdin)
    cur=d
    for p in '$2'.split('.'):
        cur=cur[int(p)] if isinstance(cur,list) else cur.get(p,{})
    if cur is None or cur=={} or cur==[]:
        print('')
    else:
        print(json.dumps(cur) if isinstance(cur,(dict,list)) else cur)
except Exception:
    print('')" 2>/dev/null
}
num() { awk "BEGIN{print ($1==\"\"?0:$1)}" 2>/dev/null || echo 0; }
diff() { awk "BEGIN{d=($1)-($2); print (d<0?-d:d)}"; }
isclose() { awk "BEGIN{print ($1<=0.01)?0:1}"; } # tolerance 0.01đ

# ── 0. Period + tenant set ──────────────────────────────────────────────────
if [ -z "$PERIOD" ]; then
  PERIOD=$(GW "SELECT to_char(max(\\\"EntryDate\\\"),'YYYY-MM') FROM \\\"JournalEntries\\\";")
fi
[ -z "$PERIOD" ] && { echo "FATAL: không xác định được period (psql lỗi?)"; exit 2; }
YEAR="${PERIOD%-*}"; MONTH="${PERIOD#*-}"
START="$YEAR-$MONTH-01"
NEXT_MONTH=$(date -d "$START +1 month" +%Y-%m-01 2>/dev/null || echo "2099-01-01")

out "════════════════════════════════════════════════════════════════════"
out "RV ACCOUNTING CONSISTENCY — period $PERIOD · gateway $GATEWAY_HOST"
out "run: $(date -u +%FT%TZ)"
out "════════════════════════════════════════════════════════════════════"

if [ ${#TENANTS[@]} -eq 0 ]; then
  mapfile -t TENANTS < <(GW "SELECT \\\"Id\\\"::text FROM \\\"Tenants\\\" t WHERE EXISTS (SELECT 1 FROM \\\"JournalEntries\\\" j WHERE j.\\\"TenantId\\\"=t.\\\"Id\\\" AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date) ORDER BY t.\\\"Name\\\" LIMIT $MAXT;")
fi
[ ${#TENANTS[@]} -eq 0 ] && { echo "FATAL: không có tenant nào có JE trong kỳ $PERIOD"; exit 2; }

# Tên + type của từng tenant
declare -A TNAME TTYPE
for T in "${TENANTS[@]}"; do
  TNAME[$T]=$(GW "SELECT \\\"Name\\\" FROM \\\"Tenants\\\" WHERE \\\"Id\\\"='$T'::uuid;")
  TTYPE[$T]=$(GW "SELECT \\\"Type\\\" FROM \\\"Tenants\\\" WHERE \\\"Id\\\"='$T'::uuid;")
  info "tenant ${TTYPE[$T]} $( [ "${TTYPE[$T]}" = "1" ] && echo "(HKD)" || echo "(DN)") : ${TNAME[$T]} — $T"
done

# ── Auth (API layer) ────────────────────────────────────────────────────────
JAR=""
if [ "$DO_API" = "1" ]; then
  JAR=$(mktemp /tmp/rv-cons-jar-XXXX)
  curl -s -c "$JAR" -X POST "$APP2/api/platform/login" -H 'Content-Type: application/json' \
    -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' >/dev/null
  # Sanity: no-auth phải 401/302
  NA=$(curl -s -o /dev/null -w '%{http_code}' "$APP2/api/trial-balances?year=$YEAR&month=$MONTH")
  chk "A0 VAS API no-auth chặn (expect 401/302/403, got $NA)" "$(echo "$NA" | grep -qE '^(401|302|403)$' && echo 0 || echo 1)" "status=$NA"
fi

impersonate_token() { # → Bearer JWT cho tenant $1 + cập nhật .VanAn.Jwt cookie
  # Re-login MỖI LẦN (curl -c ghi đè jar in-place — KHÔNG đổi path $JAR vì fn chạy trong
  # command-substitution subshell, gán biến không propagate về parent).
  curl -s -c "$JAR" -X POST "$APP2/api/platform/login" -H 'Content-Type: application/json' \
    -d '{"Username":"sysadmin@vanan.vn","Password":"2026@vanan"}' >/dev/null
  local IMP i
  for i in 1 2; do
    IMP=$(curl -s -b "$JAR" -c "$JAR" -X POST "$APP2/api/admin/impersonate/$1")
    local T; T=$(jget "$IMP" token)
    [ -n "$T" ] && { echo "$T"; return; }
    sleep 2   # cold-start / transient — retry 1 lần
  done
  echo ""
}
app2get() { # app2get <url> — cookie auth + .VanAn.Jwt impersonation (Bearer KHÔNG được app2 chấp nhận)
  curl -s -m 30 -b "$JAR" "$1"
}
app2_status() { curl -s -o /dev/null -w '%{http_code}' -m 30 -b "$JAR" "$1"; }
api2get() { # api2get <url> <token> → body (Bearer — gateway chấp nhận ShopERP JWT)
  curl -s -m 30 -H "Authorization: Bearer $2" "$1"
}

# ═══════════════════════════════════════════════════════════════════════════
# A. GROUND TRUTH (SQL) — structural integrity
# ═══════════════════════════════════════════════════════════════════════════
out ""; out "── A. Ground truth SQL (per-JE balance · period totals · balance identity) ──"
for T in "${TENANTS[@]}"; do
  TAG="${TNAME[$T]:-$(echo $T | cut -c1-8)}"

  A1=$(GW "SELECT count(*) FROM (SELECT j.\\\"Id\\\" FROM \\\"JournalEntries\\\" j JOIN \\\"JournalEntryLine\\\" l ON l.\\\"JournalEntryId\\\"=j.\\\"Id\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date GROUP BY j.\\\"Id\\\" HAVING ABS(SUM(l.\\\"DebitAmount\\\")-SUM(l.\\\"CreditAmount\\\")) > 0.01) x;")
  chk "A1[$TAG] JE lệch Nợ/Có = ${A1:-ERR} (expect 0)" "$( [ "${A1:-1}" = "0" ] && echo 0 || echo 1 )" "unbalanced=${A1:-}"

  A2=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\"),0)||'|'||COALESCE(SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date;")
  DB=$(echo "$A2" | cut -d'|' -f1); CR=$(echo "$A2" | cut -d'|' -f2); D=$(diff "$DB" "$CR")
  chk "A2[$TAG] period ΣNợ=$DB == ΣCó=$CR" "$(isclose "$D")" "diff=$D"

  # A3 identity cumulative: assets(1/2 net) == liab(3)+equity(4)+P&L(5/7−6/8 net)
  # (query riêng — FILTER chỉ bind vào aggregate cuối cùng, không dùng được trong 1 SELECT)
  A3A=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\")-SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\" ~ '^[12]';")
  L=$(GW "SELECT COALESCE(SUM(l.\\\"CreditAmount\\\")-SUM(l.\\\"DebitAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\" ~ '^3';")
  E=$(GW "SELECT COALESCE(SUM(l.\\\"CreditAmount\\\")-SUM(l.\\\"DebitAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\" ~ '^4';")
  P=$(GW "SELECT COALESCE(SUM(l.\\\"CreditAmount\\\")-SUM(l.\\\"DebitAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\" ~ '^[57]';")
  X=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\")-SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\" ~ '^[68]';")
  NET=$(awk "BEGIN{print ${P:-0}-${X:-0}}"); RHS=$(awk "BEGIN{print ${L:-0}+${E:-0}+$NET}")
  D=$(diff "${A3A:-0}" "$RHS")
  chk "A3[$TAG] Tài sản($A3A) == Nợ+Vốn+Lãi($RHS)" "$(isclose "$D")" "L=$L E=$E P&L=$NET diff=$D"

  # A4 entries↔journal revenue/expense (C3 pattern — VAT + deposit offset)
  # GAP-3 by-design (2026-11-03): loại JE bán chịu (PostingPurpose='receivable' — Nợ131/Có511,
  # Q1: bán chịu không ghi DT entry) + JE từ voucher (ReferenceType='JournalVoucher' — khấu hao/
  # ký quỹ/CTGS/vay/xuất kho/thanh lý — không tạo AccountingEntry).
  RV_J=$(GW "SELECT COALESCE(SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date AND l.\\\"AccountNumber\\\" ~ '^[57]' AND j.\\\"PostingPurpose\\\" IS DISTINCT FROM 'receivable' AND j.\\\"ReferenceType\\\" IS DISTINCT FROM 'JournalVoucher';")
  RV_E=$(GW "SELECT COALESCE(SUM(\\\"Amount\\\"),0) FROM \\\"AccountingEntries\\\" WHERE \\\"TenantId\\\"='$T'::uuid AND \\\"EntryType\\\"=1 AND \\\"AccountCode\\\" ~ '^[57]' AND \\\"TransactionDate\\\" >= '$START'::date AND \\\"TransactionDate\\\" < '$NEXT_MONTH'::date;")
  OFF_E=$(GW "SELECT COALESCE(SUM(\\\"Amount\\\"),0) FROM \\\"AccountingEntries\\\" WHERE \\\"TenantId\\\"='$T'::uuid AND \\\"EntryType\\\"=5 AND \\\"AccountCode\\\" ~ '^[57]' AND \\\"TransactionDate\\\" >= '$START'::date AND \\\"TransactionDate\\\" < '$NEXT_MONTH'::date;")
  D=$(diff "$RV_J" "$(awk "BEGIN{print $RV_E+$OFF_E}")")
  chk "A4a[$TAG] Revenue journal($RV_J) == entries($RV_E)+offset($OFF_E)" "$(isclose "$D")" "diff=$D"
  EXP_J=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date AND l.\\\"AccountNumber\\\" ~ '^[68]' AND j.\\\"ReferenceType\\\" IS DISTINCT FROM 'JournalVoucher';")
  EXP_E=$(GW "SELECT COALESCE(SUM(\\\"Amount\\\"),0) FROM \\\"AccountingEntries\\\" WHERE \\\"TenantId\\\"='$T'::uuid AND \\\"EntryType\\\"=2 AND \\\"AccountCode\\\" ~ '^[68]' AND \\\"TransactionDate\\\" >= '$START'::date AND \\\"TransactionDate\\\" < '$NEXT_MONTH'::date;")
  D=$(diff "$EXP_J" "$EXP_E")
  chk "A4b[$TAG] Expense journal($EXP_J) == entries($EXP_E)" "$(isclose "$D")" "diff=$D"
done

# ═══════════════════════════════════════════════════════════════════════════
# F. GLOBAL — isolation & integrity
# ═══════════════════════════════════════════════════════════════════════════
out ""; out "── F. Global (tenant isolation · integrity) ──"
F1=$(GW "SELECT COALESCE(SUM(t.d),0) FROM (SELECT j.\\\"TenantId\\\", SUM(l.\\\"DebitAmount\\\") d FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date GROUP BY j.\\\"TenantId\\\") t;")
F1G=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date;")
D=$(diff "$F1" "$F1G")
chk "F1 Σper-tenant debit($F1) == global($F1G)" "$(isclose "$D")" "diff=$D"
F2=$(GW "SELECT count(*) FROM \\\"JournalEntries\\\" j WHERE j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date AND NOT EXISTS (SELECT 1 FROM \\\"JournalEntryLine\\\" l WHERE l.\\\"JournalEntryId\\\"=j.\\\"Id\\\");")
chk "F2 JE không có line = ${F2:-ERR} (expect 0)" "$( [ "${F2:-1}" = "0" ] && echo 0 || echo 1 )" "empty=${F2:-}"
F3=$(GW "SELECT count(*) FROM \\\"JournalEntries\\\" j JOIN \\\"JournalEntryLine\\\" l ON l.\\\"JournalEntryId\\\"=j.\\\"Id\\\" WHERE j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date AND (l.\\\"DebitAmount\\\" < 0 OR l.\\\"CreditAmount\\\" < 0);")
chk "F3 JE line âm = ${F3:-ERR} (expect 0)" "$( [ "${F3:-1}" = "0" ] && echo 0 || echo 1 )" "negative=${F3:-}"

# ═══════════════════════════════════════════════════════════════════════════
# B/C/D/E per-tenant — API reports vs SQL + sổ sách + dashboard + công nợ
# ═══════════════════════════════════════════════════════════════════════════
for T in "${TENANTS[@]}"; do
  TAG="${TNAME[$T]:-$(echo $T | cut -c1-8)}"
  IS_ENT=0; case "${TTYPE[$T]}" in 2|3|4) IS_ENT=1;; esac
  out ""; out "── Tenant: $TAG ($T) — Type '${TTYPE[$T]}' ──"

  # ── SQL truth cho tenant/kỳ này ──
  # Movement kỳ (TB basis — TẤT CẢ JE kể cả closing/reversal, services không filter)
  MV_D=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date;")
  MV_C=$(GW "SELECT COALESCE(SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date;")
  # P&L net kỳ (IS basis — cùng dataset không filter)
  PL_NET=$(GW "SELECT COALESCE(SUM(l.\\\"CreditAmount\\\")-SUM(l.\\\"DebitAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date AND l.\\\"AccountNumber\\\" ~ '^[57]';")
  PL_X=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\")-SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date AND l.\\\"AccountNumber\\\" ~ '^[68]';")
  NET_PROFIT=$(awk "BEGIN{print $PL_NET-$PL_X}")
  # Assets cumulative (BS basis)
  ASSETS=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\")-SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\" ~ '^[12]';")
  # Cash 111/112 (CFS basis): opening (<START) + closing cumulative + movement
  CASH_OPEN=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\")-SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\" ~ '^11[12]' AND j.\\\"EntryDate\\\" < '$START'::date;")
  CASH_MV=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\")-SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\" ~ '^11[12]' AND j.\\\"EntryDate\\\" >= '$START'::date AND j.\\\"EntryDate\\\" < '$NEXT_MONTH'::date;")
  CASH_CLOSE=$(awk "BEGIN{print $CASH_OPEN+$CASH_MV}")
  # closing/reversal presence (giải thích IS=0 nếu kỳ đã đóng)
  N_CLOSE=$(GW "SELECT count(*) FROM \\\"JournalEntries\\\" WHERE \\\"TenantId\\\"='$T'::uuid AND \\\"EntryDate\\\" >= '$START'::date AND \\\"EntryDate\\\" < '$NEXT_MONTH'::date AND \\\"PostingPurpose\\\"='closing';")
  [ "${N_CLOSE:-0}" != "0" ] && info "[$TAG] kỳ đã kết chuyển (${N_CLOSE} closing JEs) — P&L accounts đã zeroed, IS/B02 = 0 là đúng"

  # ── B. VAS report APIs (cookie auth — Bearer impersonated JWT bị app2 reject 302) ──
  TOK=""; IS_REV=""
  [ "$DO_API" = "1" ] && TOK=$(impersonate_token "$T")
  if [ "$DO_API" = "1" ]; then
    if [ "$IS_ENT" = "0" ]; then
      # Non-enterprise (HKD Type=1 / Type NULL): VAS bị chặn 403 theo feature flag — check đúng contract
      ST=$(app2_status "$APP2/api/trial-balances?year=$YEAR&month=$MONTH")
      chk "B0[$TAG] VAS API chặn non-enterprise (expect 403, got $ST)" "$( [ "$ST" = "403" ] && echo 0 || echo 1 )" "status=$ST type='${TTYPE[$T]}'"
    elif [ -z "$TOK" ]; then
      chk "B0[$TAG] impersonate → JWT" 1 "no token"
    else
      # B1 Trial Balance — Sổ Tổng hợp
      TB=$(app2get "$APP2/api/trial-balances?year=$YEAR&month=$MONTH" "$TOK")
      TB_D=$(jget "$TB" totalDebit); TB_C=$(jget "$TB" totalCredit); TB_BAL=$(jget "$TB" isBalanced)
      D=$(diff "${TB_D:-0}" "$MV_D"); chk "B1a[$TAG] TB.totalDebit($TB_D) == ΣNợ kỳ SQL($MV_D)" "$(isclose "$D")" "diff=$D"
      D=$(diff "${TB_C:-0}" "$MV_C"); chk "B1b[$TAG] TB.totalCredit($TB_C) == ΣCó kỳ SQL($MV_C)" "$(isclose "$D")" "diff=$D"
      chk "B1c[$TAG] TB.isBalanced = $TB_BAL (expect true)" "$( [ "$TB_BAL" = "True" ] || [ "$TB_BAL" = "true" ] && echo 0 || echo 1 )" "isBalanced=$TB_BAL"

      # B2 Sổ cái spot-check: TB per-account balance == SQL cumulative
      for AC in 111 112 131 331 421 511 632; do
        ACC_BAL=$(jget "$TB" "accounts" | python3 -c "import sys,json
for a in json.load(sys.stdin):
    if a.get('accountNumber')=='$AC': print(a.get('balance')); break" 2>/dev/null)
        [ -z "$ACC_BAL" ] && continue   # account không có phát sinh → skip
        SQL_BAL=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\")-SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\"='$AC';")
        D=$(diff "$ACC_BAL" "$SQL_BAL")
        chk "B2[$TAG] Sổ cái $AC: TB.balance($ACC_BAL) == SQL dư lũy kế($SQL_BAL)" "$(isclose "$D")" "diff=$D"
      done

      # B3 Income Statement — B02: NetProfitEnding == P&L net SQL (same unfiltered dataset)
      IS=$(app2get "$APP2/api/income-statements?year=$YEAR&month=$MONTH" "$TOK")
      IS_NP=$(jget "$IS" netProfitEnding); IS_REV=$(jget "$IS" totalRevenueEnding)
      D=$(diff "${IS_NP:-0}" "$NET_PROFIT")
      chk "B3a[$TAG] B02 NetProfit($IS_NP) == SQL P&L net($NET_PROFIT)" "$(isclose "$D")" "diff=$D revAPI=$IS_REV revSQL=$PL_NET"
      # B3b internal: NetProfit == revenue − (COGS+OpEx+other) — check qua cogs/opex fields
      IS_COGS=$(jget "$IS" totalCogsEnding); IS_OPEX=$(jget "$IS" totalOpExEnding)
      [ -n "$IS_COGS" ] && [ -n "$IS_OPEX" ] && [ -n "$IS_REV" ] && \
        info "[$TAG] B02 detail: revenue=$IS_REV cogs=$IS_COGS opex=$IS_OPEX → net=$IS_NP"

      # B4 Balance Sheet — B01: internal invariant + assets vs SQL
      BS=$(app2get "$APP2/api/balance-sheets?year=$YEAR&month=$MONTH" "$TOK")
      BS_A=$(jget "$BS" totalAssetsEnding); BS_LE=$(jget "$BS" totalLiabilitiesAndEquityEnding)
      chk "B4a[$TAG] B01 invariant: assets($BS_A) == L+E($BS_LE)" "$(isclose "$(diff "${BS_A:-0}" "${BS_LE:-0}")")" "diff=$(diff "${BS_A:-0}" "${BS_LE:-0}")"
      D=$(diff "${BS_A:-0}" "$ASSETS")
      # B01 assets = 1xx+2xx net — cộng 9xx? (911 kết chuyển lỗ → Nợ 421 giảm vốn, vẫn class 4) — check trực tiếp
      chk "B4b[$TAG] B01 assets($BS_A) == SQL tài sản lũy kế($ASSETS)" "$(isclose "$D")" "diff=$D"

      # B5 Cash Flow — B03: NetChange + ClosingCash vs SQL 111/112
      CFS=$(app2get "$APP2/api/cash-flow-statements?year=$YEAR&month=$MONTH" "$TOK")
      CFS_NC=$(jget "$CFS" netChange); CFS_CC=$(jget "$CFS" closingCash); CFS_OC=$(jget "$CFS" openingCash)
      D=$(diff "${CFS_NC:-0}" "$CASH_MV")
      chk "B5a[$TAG] B03 NetChange($CFS_NC) == SQL cash movement($CASH_MV)" "$(isclose "$D")" "diff=$D"
      D=$(diff "${CFS_CC:-0}" "$CASH_CLOSE")
      chk "B5b[$TAG] B03 ClosingCash($CFS_CC) == SQL dư tiền lũy kế($CASH_CLOSE)" "$(isclose "$D")" "diff=$D (open=$CFS_OC vs $CASH_OPEN)"
    fi

    # ── D. Báo cáo quản trị: profit-summary (api2) — delegates IncomeStatement.TotalRevenueEnding ──
    PS=$(api2get "$API2/api/financial/profit-summary?period=$PERIOD" "$TOK")
    PS_RV=$(jget "$PS" revenue)
    if [ -z "$PS_RV" ]; then PS_RV=$(jget "$PS" totalRevenue); fi
    if [ -n "$PS_RV" ] && [ -n "${IS_REV:-}" ]; then
      D=$(diff "$PS_RV" "$IS_REV")
      chk "D2a[$TAG] profit-summary revenue($PS_RV) == B02 revenue($IS_REV)" "$(isclose "$D")" "diff=$D"
    elif [ -n "$PS_RV" ]; then
      D=$(diff "$PS_RV" "$PL_NET")
      chk "D2b[$TAG] profit-summary revenue($PS_RV) == SQL P&L rev($PL_NET)" "$(isclose "$D")" "diff=$D"
    else
      warn "[$TAG] profit-summary không trả revenue — $(echo "$PS" | head -c 120)"
    fi
  fi

  # ── D1. Dashboard kế toán (AccountingIndex — entries formula) ──
  DI_RV=$(GW "SELECT COALESCE(SUM(\\\"Amount\\\"),0) FROM \\\"AccountingEntries\\\" WHERE \\\"TenantId\\\"='$T'::uuid AND \\\"EntryType\\\" IN (1,5) AND \\\"AccountCode\\\" ~ '^[57]' AND \\\"TransactionDate\\\" >= '$START'::date AND \\\"TransactionDate\\\" < '$NEXT_MONTH'::date;")
  DI_EX=$(GW "SELECT COALESCE(SUM(\\\"Amount\\\"),0) FROM \\\"AccountingEntries\\\" WHERE \\\"TenantId\\\"='$T'::uuid AND \\\"EntryType\\\"=2 AND \\\"AccountCode\\\" ~ '^[68]' AND \\\"TransactionDate\\\" >= '$START'::date AND \\\"TransactionDate\\\" < '$NEXT_MONTH'::date;")
  DI_NP=$(awk "BEGIN{print $DI_RV-$DI_EX}")
  info "[$TAG] Dashboard AccountingIndex: revenue=$DI_RV expense=$DI_EX netProfit=$DI_NP (entries — so với journal rev=$PL_NET net=$NET_PROFIT)"

  # ── E. Công nợ: entries 131/331 vs journal 131/331 (cumulative) ──
  # GAP-4 by-design (2026-11-03): HKD (type 1) + DNSN (type 2) = cash-basis — bridge KHÔNG
  # tạo JE cho công nợ (ManualEntryJournalBridge G-C4) → entries-only là đúng → skip.
  # DN/HTX (3/4/5): chỉ tạo JE khi flag AccrualReceivableJE bật (SystemSettings key
  # "ShopFeature:{tenant}:AccrualReceivableJE"='true'); payable-create luôn Skipped
  # ("needs-counter-account") → E2 chỉ so receivable-side JEs kỳ vọng, đánh giá bằng mắt.
  ACCRUAL_ON=0
  case "${TTYPE[$T]}" in
    3|4|5)
      FLAG=$(GW "SELECT COALESCE(\\\"Value\\\",'false') FROM \\\"SystemSettings\\\" WHERE \\\"Key\\\"='ShopFeature:$T:AccrualReceivableJE' LIMIT 1;")
      [ "$FLAG" = "true" ] && ACCRUAL_ON=1;;
    *) ;;
  esac
  E131_E=$(GW "SELECT COALESCE(SUM(\\\"Amount\\\"),0) FROM \\\"AccountingEntries\\\" WHERE \\\"TenantId\\\"='$T'::uuid AND \\\"AccountCode\\\"='131';")
  E131_J=$(GW "SELECT COALESCE(SUM(l.\\\"DebitAmount\\\")-SUM(l.\\\"CreditAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\"='131';")
  E331_E=$(GW "SELECT COALESCE(SUM(\\\"Amount\\\"),0) FROM \\\"AccountingEntries\\\" WHERE \\\"TenantId\\\"='$T'::uuid AND \\\"AccountCode\\\"='331';")
  E331_J=$(GW "SELECT COALESCE(SUM(l.\\\"CreditAmount\\\")-SUM(l.\\\"DebitAmount\\\"),0) FROM \\\"JournalEntryLine\\\" l JOIN \\\"JournalEntries\\\" j ON j.\\\"Id\\\"=l.\\\"JournalEntryId\\\" WHERE j.\\\"TenantId\\\"='$T'::uuid AND l.\\\"AccountNumber\\\"='331';")
  if [ "$ACCRUAL_ON" = "1" ]; then
    D=$(diff "$(awk "BEGIN{d=$E131_E; print (d<0?-d:d)}")" "$(awk "BEGIN{d=$E131_J; print (d<0?-d:d)}")")
    chk "E1[$TAG] |entries 131|($E131_E) ≈ |journal 131 net|($E131_J)" "$(isclose "$D")" "diff=$D"
    D=$(diff "$(awk "BEGIN{d=$E331_E; print (d<0?-d:d)}")" "$(awk "BEGIN{d=$E331_J; print (d<0?-d:d)}")")
    chk "E2[$TAG] |entries 331|($E331_E) ≈ |journal 331 net|($E331_J)" "$(isclose "$D")" "diff=$D"
  else
    info "[$TAG] E1/E2 skip — công nợ không tạo JE (cash-basis HKD/DNSN hoặc AccrualReceivableJE OFF): entries 131=$E131_E / journal=$E131_J · entries 331=$E331_E / journal=$E331_J"
  fi
done

[ -n "$JAR" ] && rm -f "$JAR"
out ""
out "════════════════════════════════════════════════════════════════════"
out "RV CONSISTENCY SUMMARY: $PASS PASS · $FAIL FAIL · $WARN WARN — report: $REPORT"
out "════════════════════════════════════════════════════════════════════"
[ "$FAIL" = "0" ]
