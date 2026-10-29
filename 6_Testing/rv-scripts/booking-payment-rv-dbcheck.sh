#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# RV DB-CHECK — booking-payment sweep (issue #195, 243112db)
# Đọc rv-scripts/booking-payment-rv-report.json → verify 3 tầng:
#   PG: booking.OrderId · KHÔNG entries Booking:{id} (single-source guard) · order Pending
#   SQLite shop-a: order mirror SourceType=booking · chưa có entries Order:{id} trước "Thanh toán"
# Chạy: bash rv-scripts/booking-payment-rv-dbcheck.sh (sau sweep — report phải tồn tại)
# ─────────────────────────────────────────────────────────────────────────────
set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPORT="$SCRIPT_DIR/booking-payment-rv-report.json"
[ -f "$REPORT" ] || { echo "Report missing: $REPORT — chạy sweep trước"; exit 1; }
# Git Bash path (/c/...) → Windows path (C:/...) cho python Windows
REPORT_WIN=$(echo "$REPORT" | sed 's|^/\([a-zA-Z]\)/|\1:/|')

BOOKING=$(python3 -c "import json;print(json.load(open('$REPORT_WIN'))['bookingId'] or '')")
ORDER=$(python3 -c "import json;print(json.load(open('$REPORT_WIN'))['orderId'] or '')")
echo "bookingId=$BOOKING orderId=$ORDER"
[ -n "$BOOKING" ] && [ -n "$ORDER" ] || { echo "Sweep chưa tạo được booking/order — skip DB check"; exit 1; }

PASS=0; FAIL=0
chk() { if [ "$2" = "0" ]; then echo "  [PASS] $1"; PASS=$((PASS+1)); else echo "  [FAIL] $1 — $3"; FAIL=$((FAIL+1)); fi; }

# ── G. PG (gateway) ─────────────────────────────────────────────────────────
echo "\n── G. PG checks ──"
GW() { gcloud compute ssh vanan-gateway --zone asia-southeast1-a --project vanan-prod --command "docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -t -A -c \"$1\"" 2>/dev/null; }

# G1: booking.OrderId == orderId (AttachOrder đúng)
ORD_LINK=$(GW "SELECT \\\"OrderId\\\" FROM \\\"Bookings\\\" WHERE \\\"Id\\\" = '$BOOKING';")
chk "G1 Booking.OrderId == orderId ($ORD_LINK)" "$( [ "$ORD_LINK" = "$ORDER" ] && echo 0 || echo 1 )" "booking.OrderId='$ORD_LINK' order='$ORDER'"

# G2: KHÔNG entries Booking:{id} — skip guard hoạt động (single source)
CNT_BK=$(GW "SELECT COUNT(*) FROM \\\"AccountingEntries\\\" WHERE \\\"Reference\\\" LIKE 'Booking:$BOOKING%';")
chk "G2 Không có revenue Booking:{id} (skip guard) — count=$CNT_BK" "$( [ "$CNT_BK" = "0" ] && echo 0 || echo 1 )" "entries=$CNT_BK"

# G3: PG order — SourceType=booking · Status=pending · PaymentStatus=Pending (CHỈ pre-payment)
# G3/S1/S2 là assert trạng thái TRƯỚC "Thanh toán" — bỏ qua khi chạy --after-payment (đơn đã Paid).
if [ "${1:-}" != "--after-payment" ]; then
  ROW=$(GW "SELECT \\\"SourceType\\\"||'|'||\\\"Status\\\"||'|'||\\\"PaymentStatus\\\" FROM \\\"Orders\\\" WHERE \\\"Id\\\" = '$ORDER';")
  chk "G3 PG Order booking|pending|Pending ($ROW)" "$( [ "$ROW" = "booking|pending|Pending" ] && echo 0 || echo 1 )" "row='$ROW'"
fi

# ── S. SQLite shop-a ────────────────────────────────────────────────────────
echo "\n── S. SQLite checks (shop-a) ──"
SQ() { gcloud compute ssh vanan-shop-a --zone asia-southeast1-b --project vanan-prod --command "sudo python3 -c \"
import sqlite3
db = sqlite3.connect('/var/lib/docker/volumes/vanan_shoperp_data/_data/vanan_shoperp.db', timeout=30)
cur = db.cursor()
cur.execute(\\\"$1\\\")
row = cur.fetchone()
print(row[0] if row is not None else '')
db.close()
\"" 2>/dev/null; }

# S1: order mirror có mặt + SourceType=booking + pending + Pending (CHỈ pre-payment)
if [ "${1:-}" != "--after-payment" ]; then
  S1=$(SQ "SELECT SourceType||'|'||Status||'|'||PaymentStatus FROM Orders WHERE Id='${ORDER^^}'")
  chk "S1 SQLite Order booking|pending|Pending ($S1)" "$( [ "$S1" = "booking|pending|Pending" ] && echo 0 || echo 1 )" "row='$S1'"

  # S2: chưa có revenue Order:{id} trước 'Thanh toán' — AccountingEntries ở PG (Gateway = accounting source of truth)
  S2=$(GW "SELECT COUNT(*) FROM \\\"AccountingEntries\\\" WHERE \\\"Reference\\\" = '$ORDER';")
  chk "S2 Chưa có revenue Order:{id} trước 'Thanh toán' — count=$S2" "$( [ "$S2" = "0" ] && echo 0 || echo 1 )" "entries=$S2"
fi

# ── AFTER-PAYMENT (chạy với --after-payment sau khi UI bấm "Thanh toán") ──
if [ "${1:-}" = "--after-payment" ]; then
  echo "\n── Post-payment checks (sau UI 'Thanh toán') ──"

  # S3: revenue entries ĐÃ tạo (PG — Reference = orderId)
  S3=$(GW "SELECT COUNT(*) FROM \\\"AccountingEntries\\\" WHERE \\\"Reference\\\" = '$ORDER';")
  chk "S3 Revenue Order:{id} đã tạo sau 'Thanh toán' — count=$S3" "$( [ "$S3" -gt 0 ] && echo 0 || echo 1 )" "entries=$S3"

  # S4: accounts gồm 511 (revenue) + 3331 (VAT) + 632 (COGS)
  S4=$(GW "SELECT string_agg(DISTINCT \\\"AccountCode\\\", ',') FROM \\\"AccountingEntries\\\" WHERE \\\"Reference\\\" = '$ORDER';")
  has511=$(echo "$S4" | tr ',' '\n' | grep -c '^511$')
  has3331=$(echo "$S4" | tr ',' '\n' | grep -c '^3331$')
  has632=$(echo "$S4" | tr ',' '\n' | grep -c '^632$')
  chk "S4 Accounts 511+3331+632 ($S4)" "$( [ "$has511" -gt 0 ] && [ "$has3331" -gt 0 ] && [ "$has632" -gt 0 ] && echo 0 || echo 1 )" "accounts=$S4"

  # G4: PG vẫn KHÔNG có Booking:{id} revenue (no double — single source)
  G4=$(GW "SELECT COUNT(*) FROM \\\"AccountingEntries\\\" WHERE \\\"Reference\\\" LIKE 'Booking:$BOOKING%';")
  chk "G4 Vẫn không có revenue Booking:{id} — count=$G4" "$( [ "$G4" = "0" ] && echo 0 || echo 1 )" "entries=$G4"

  # G5: PG order PaymentStatus=Paid + Status=confirmed
  G5=$(GW "SELECT \\\"Status\\\"||'|'||\\\"PaymentStatus\\\" FROM \\\"Orders\\\" WHERE \\\"Id\\\" = '$ORDER';")
  chk "G5 PG Order confirmed|Paid ($G5)" "$( [ "$G5" = "confirmed|Paid" ] && echo 0 || echo 1 )" "row='$G5'"

  # ── Issue #195 double-revenue journal regression (fix 21505b38) ──
  # G6: KHÔNG còn journal trực tiếp ref 'Order' (double-revenue path đã bỏ — bridge single source)
  G6=$(GW "SELECT COUNT(*) FROM \\\"JournalEntries\\\" WHERE \\\"ReferenceType\\\" = 'Order' AND \\\"ReferenceId\\\" = '$ORDER';")
  chk "G6 KHÔNG journal ref 'Order' (double path removed) — count=$G6" "$( [ "$G6" = "0" ] && echo 0 || echo 1 )" "journals=$G6"

  # G7: journal = ĐÚNG 1 bộ/order (2: revenue Nợ 111/Có 511 + COGS Nợ 632/Có 111) — bridge tạo từ entries
  G7=$(GW "SELECT COUNT(*) FROM \\\"JournalEntries\\\" WHERE \\\"ReferenceId\\\" IN (SELECT \\\"Id\\\" FROM \\\"AccountingEntries\\\" WHERE \\\"Reference\\\" = '$ORDER');")
  chk "G7 Journal ĐÚNG 1 bộ/order — count=$G7 (expect 2: revenue + COGS)" "$( [ "$G7" = "2" ] && echo 0 || echo 1 )" "journals=$G7"
fi

# ── DEPOSIT (chạy với --deposit sau sweep có deposit scenario) ──
# Verify DepositPaidAmount snapshot (issue #195 follow-up 21505b38): order từ booking có cọc đã thu.
if [ "${1:-}" = "--deposit" ]; then
  DEPOSIT_ORDER=$(python3 -c "import json;print(json.load(open('$REPORT_WIN'))['depositOrderId'] or '')")
  DEPOSIT_AMOUNT=$(python3 -c "import json;print(json.load(open('$REPORT_WIN'))['depositAmount'] or 0)")
  echo "\n── Deposit checks (DepositPaidAmount snapshot) ──"
  if [ -n "$DEPOSIT_ORDER" ]; then
    D1=$(GW "SELECT \\\"DepositPaidAmount\\\" FROM \\\"Orders\\\" WHERE \\\"Id\\\" = '$DEPOSIT_ORDER';")
    chk "D1 PG Order.DepositPaidAmount == $DEPOSIT_AMOUNT (actual=$D1)" "$( [ "$D1" = "$DEPOSIT_AMOUNT" ] && echo 0 || echo 1 )" "amount='$D1'"
  else
    chk "D1 (skip — no deposit order in report)" 1 "depositOrderId empty"
  fi
fi

echo "\nDB-CHECK SUMMARY: $PASS PASS · $FAIL FAIL"
[ "$FAIL" = "0" ]