#!/bin/bash
# RV — Phiếu chứng từ kế toán (2026-11-01)
# Chạy TRÊN VPS vanan-shop-a: kiểm tra routes live + markers trong DLL production.
# Usage: gcloud compute ssh vanan-shop-a --zone asia-southeast1-b --project vanan-prod --command "bash -s" < rv-phieu-chung-tu.sh

APP="https://app2.khachvip.online"
PASS=0; FAIL=0
ck() { if [ "$2" = "$3" ]; then echo "  PASS $1"; PASS=$((PASS+1)); else echo "  FAIL $1 (got='$2' want='$3')"; FAIL=$((FAIL+1)); fi; }
ckn() { if [ "$2" -ge "$3" ] 2>/dev/null; then echo "  PASS $1 (count=$2)"; PASS=$((PASS+1)); else echo "  FAIL $1 (count=$2 < $3)"; FAIL=$((FAIL+1)); fi; }

echo "=== A. ROUTES (expect 302 — route exists, redirect login) ==="
for r in accounting accounting/revenue accounting/expenses accounting/journal-voucher \
         accounting/purchase-receipts accounting/opening-balance accounting/cong-no \
         accounting/history accounting/mapping-rules accounting/cash-book accounting/bank-book \
         accounting/journal accounting/ledger accounting/sub-ledger accounting/balance \
         accounting/import accounting/period-closing accounting/cash-payment-review \
         accounting/financial-reports accounting/hkd-books; do
  code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 15 "$APP/$r")
  ck "route /$r" "$code" "302"
done

echo "=== B. MARKERS ShopERP.dll (voucher pages + print components + menu links) ==="
C=vanan-shoperp-1
for m in JournalVoucher JournalVoucherPrint Tt71ReceiptVoucher Tt71PaymentVoucher \
         RevenueEntry ExpenseEntry TransactionHistory PurchaseReceipts OpeningBalance \
         MappingRules CashBook BankBook JournalReport CongNoReport; do
  n=$(docker exec $C grep -c "$m" /app/VanAn.ShopERP.dll 2>/dev/null)
  ckn "ShopERP.dll marker $m" "${n:-0}" 1
done
# Menu links mới (titles — UTF-16 không grep được, dùng route strings ASCII)
for m in "journal-voucher" "mapping-rules" "cash-book" "bank-book" "revenue" "expenses"; do
  n=$(docker exec $C grep -c "accounting/$m" /app/VanAn.ShopERP.dll 2>/dev/null)
  ckn "menu link /accounting/$m" "${n:-0}" 1
done

echo "=== C. MARKERS CoreHub.dll (services + voucher types) ==="
for m in ManualJournalVoucherService CongNoService PurchaseReceiptService OpeningBalanceService \
         InternalTransferService VoucherNumberService ManualEntryJournalBridge; do
  n=$(docker exec $C grep -c "$m" /app/VanAn.CoreHub.dll 2>/dev/null)
  ckn "CoreHub.dll marker $m" "${n:-0}" 1
done
# Voucher type consts (tên type UTF-8 metadata — giá trị "CTGS" UTF-16 không grep được)
for m in TypeJournalVoucher TypePurchaseReceipt TypeReceiptCongNo TypePaymentCongNo TypeTransfer; do
  n=$(docker exec $C grep -c "$m" /app/VanAn.CoreHub.dll 2>/dev/null)
  ckn "voucher const $m" "${n:-0}" 1
done

echo "=== D. DEPLOYED IMAGE ==="
img=$(docker ps --format '{{.Image}}' -f name=$C)
echo "  $img"

echo ""
echo "RESULT: PASS=$PASS FAIL=$FAIL"
