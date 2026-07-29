#!/bin/bash
# RV5 — Sprint 5 (Wallet + COD + Settlement) Release Verification
# Run on VPS: bash rv5_all.sh

PASS=0
FAIL=0
TOTAL=0

check() {
  TOTAL=$((TOTAL+1))
  if [ "$2" = "$3" ]; then
    echo "  [PASS] $1 (got: $2)"
    PASS=$((PASS+1))
  else
    echo "  [FAIL] $1 (expected: $3, got: $2)"
    FAIL=$((FAIL+1))
  fi
}

check_contains() {
  TOTAL=$((TOTAL+1))
  if echo "$2" | grep -q "$3"; then
    echo "  [PASS] $1 (found: $3)"
    PASS=$((PASS+1))
  else
    echo "  [FAIL] $1 (expected to contain: $3, got: $2)"
    FAIL=$((FAIL+1))
  fi
}

echo "============================================"
echo "RV5: Sprint 5 — Wallet + COD + Settlement"
echo "============================================"
echo ""

# === RV5-1: Container health ===
echo "=== RV5-1: Container health ==="
for svc in gateway shoperp khachlink postgres; do
  status=$(docker inspect --format='{{.State.Health.Status}}' vanan-$svc 2>/dev/null || echo "n/a")
  check "vanan-$svc health" "$status" "healthy"
done
echo ""

# === RV5-2: Backend API checks (no-token → 401) ===
echo "=== RV5-2: Backend API checks (no-token → 401) ==="

# RV5-2a: GET /api/community/wallet (no token)
code=$(curl -sk -o /dev/null -w "%{http_code}" "https://api.khachvip.online/api/community/wallet")
check "GET /api/community/wallet (no token)" "$code" "401"

# RV5-2b: POST /api/community/wallet/confirm-cod (no token)
code=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "https://api.khachvip.online/api/community/wallet/confirm-cod" -H "Content-Type: application/json" -d '{"orderId":"00000000-0000-0000-0000-000000000099","amount":50000}')
check "POST /api/community/wallet/confirm-cod (no token)" "$code" "401"

# RV5-2c: POST /api/community/wallet/confirm-advance (no token)
code=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "https://api.khachvip.online/api/community/wallet/confirm-advance" -H "Content-Type: application/json" -d '{"orderId":"00000000-0000-0000-0000-000000000099","amount":30000}')
check "POST /api/community/wallet/confirm-advance (no token)" "$code" "401"

# RV5-2d: GET /api/community/wallet/pending-advances (no token)
code=$(curl -sk -o /dev/null -w "%{http_code}" "https://api.khachvip.online/api/community/wallet/pending-advances")
check "GET /api/community/wallet/pending-advances (no token)" "$code" "401"

# RV5-2e: POST /api/community/wallet/confirm-advance-received (no token)
code=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "https://api.khachvip.online/api/community/wallet/confirm-advance-received" -H "Content-Type: application/json" -d '{"advanceTransactionId":"00000000-0000-0000-0000-000000000099"}')
check "POST /api/community/wallet/confirm-advance-received (no token)" "$code" "401"
echo ""

# === RV5-3: DLL check (WalletService compiled in Gateway) ===
echo "=== RV5-3: DLL check (WalletService in Gateway) ==="
result=$(docker exec vanan-gateway sh -c 'strings /app/VanAn.CoreHub.dll 2>/dev/null | grep -E "WalletService|ConfirmCodAsync|ConfirmAdvanceAsync|ConfirmAdvanceReceivedAsync|GetPendingAdvancesAsync|ReverseTransactionAsync|GetWalletAsync|MarkCodCollected" | sort -u')
check_contains "WalletService methods in VanAn.CoreHub.dll" "$result" "ConfirmCodAsync"
check_contains "WalletService methods in VanAn.CoreHub.dll" "$result" "ConfirmAdvanceReceivedAsync"
check_contains "WalletService methods in VanAn.CoreHub.dll" "$result" "ReverseTransactionAsync"
check_contains "WalletService methods in VanAn.CoreHub.dll" "$result" "MarkCodCollected"

# CommunityController wallet routes compiled
result=$(docker exec vanan-gateway sh -c 'strings /app/VanAn.Gateway.dll 2>/dev/null | grep -E "wallet/confirm-cod|wallet/confirm-advance|wallet/pending-advances|wallet/confirm-advance-received|api/community/wallet" | sort -u')
check_contains "Wallet routes in VanAn.Gateway.dll" "$result" "wallet/confirm-cod"
check_contains "Wallet routes in VanAn.Gateway.dll" "$result" "wallet/pending-advances"
echo ""

# === RV5-4: KhachLink page route (Wallet.razor) ===
echo "=== RV5-4: KhachLink page routes ==="
code=$(curl -sk -o /dev/null -w "%{http_code}" "https://diemthuong.khachvip.online/community/wallet")
check "KhachLink /community/wallet" "$code" "200"

# Verify Wallet page compiled in WASM dll
result=$(docker exec vanan-khachlink sh -c 'KL=$(find /usr/share/nginx/html -name "VanAn.KhachLink.dll" | head -1); grep -aoE "Wallet|WalletHttpService|ConfirmCodAsync|ConfirmAdvanceAsync|GetWalletAsync|PendingAdvances" "$KL" 2>/dev/null | sort -u')
check_contains "Wallet page in KhachLink WASM dll" "$result" "Wallet"
check_contains "WalletHttpService in KhachLink WASM dll" "$result" "WalletHttpService"

# Regression: existing pages still work
code=$(curl -sk -o /dev/null -w "%{http_code}" "https://diemthuong.khachvip.online/")
check "KhachLink / (regression)" "$code" "200"

code=$(curl -sk -o /dev/null -w "%{http_code}" "https://diemthuong.khachvip.online/community/nearby-products")
check "KhachLink /community/nearby-products (regression)" "$code" "200"

code=$(curl -sk -o /dev/null -w "%{http_code}" "https://diemthuong.khachvip.online/community/sales-dashboard")
check "KhachLink /community/sales-dashboard (regression)" "$code" "200"
echo ""

# === RV5-5: DB check (WalletTransactions table) ===
echo "=== RV5-5: DB check (WalletTransactions table) ==="
result=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "\dt" 2>&1 | grep -iE "WalletTransactions")
check_contains "WalletTransactions table exists" "$result" "WalletTransactions"

# Verify columns (CodCollectedAt was added by Sprint 0, MarkCodCollected uses it)
result=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "\d \"Orders\"" 2>&1 | grep -iE "CodAmount|CodCollectedAt")
check_contains "Orders.CodAmount column" "$result" "CodAmount"
check_contains "Orders.CodCollectedAt column" "$result" "CodCollectedAt"

# WalletTransactionType enum values (verify via existing data or just table structure)
result=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "\d \"WalletTransactions\"" 2>&1 | grep -iE "Type|Amount|BalanceAfter|OwnerId|RelatedOrderId|RelatedTransactionId")
check_contains "WalletTransactions.Type column" "$result" "Type"
check_contains "WalletTransactions.BalanceAfter column" "$result" "BalanceAfter"
echo ""

# === RV5-6: Regression Sprint 1-4 endpoints ===
echo "=== RV5-6: Regression Sprint 1-4 endpoints ==="

# Sprint 1: nearby-orders
code=$(curl -sk -o /dev/null -w "%{http_code}" "https://api.khachvip.online/api/community/nearby-orders?lat=10.8&lng=106.7&radiusKm=5")
check "S1: GET /api/community/nearby-orders (no token)" "$code" "401"

# Sprint 2: delivery workflow
code=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "https://api.khachvip.online/api/community/orders/00000000-0000-0000-0000-000000000099/pickup")
check "S2: POST /api/community/orders/{id}/pickup (no token)" "$code" "401"

# Sprint 3: chat
code=$(curl -sk -o /dev/null -w "%{http_code}" "https://api.khachvip.online/api/community/chat/conversations/00000000-0000-0000-0000-000000000099")
check "S3: GET /api/community/chat/conversations/{id} (no token)" "$code" "401"

# Sprint 4: salesman
code=$(curl -sk -o /dev/null -w "%{http_code}" "https://api.khachvip.online/api/community/nearby-products?lat=10.8&lng=106.7&radiusKm=10")
check "S4: GET /api/community/nearby-products (no token)" "$code" "401"

code=$(curl -sk -o /dev/null -w "%{http_code}" "https://api.khachvip.online/api/community/salesman/qr?productId=00000000-0000-0000-0000-000000000001")
check "S4: GET /api/community/salesman/qr (no token)" "$code" "401"

code=$(curl -sk -o /dev/null -w "%{http_code}" "https://api.khachvip.online/api/community/salesman/commissions")
check "S4: GET /api/community/salesman/commissions (no token)" "$code" "401"

# Sprint 4: admin referral configs
code=$(curl -sk -o /dev/null -w "%{http_code}" "https://api.khachvip.online/api/admin/products/referral-configs")
check "S4: GET /api/admin/products/referral-configs (no auth)" "$code" "401"

# ShopERP admin page (regression)
code=$(curl -sk -o /dev/null -w "%{http_code}" -L --max-redirs 0 "https://khachvip.online/admin/product-referral-configs")
check "S4: ShopERP /admin/product-referral-configs (no auth → 302)" "$code" "302"
echo ""

# === RV5-7: Gateway logs — no Sprint 5 startup errors ===
echo "=== RV5-7: Gateway logs — Sprint 5 startup errors ==="
errors=$(docker logs vanan-gateway --since 3h 2>&1 | grep -iE "error|exception|fail" | grep -iE "wallet|cod|advance|settlement|reverse" | head -10)
if [ -z "$errors" ]; then
  TOTAL=$((TOTAL+1))
  PASS=$((PASS+1))
  echo "  [PASS] No Sprint 5 startup errors in Gateway logs"
else
  TOTAL=$((TOTAL+1))
  FAIL=$((FAIL+1))
  echo "  [FAIL] Sprint 5 errors found in Gateway logs:"
  echo "$errors"
fi
echo ""

# === SUMMARY ===
echo "============================================"
echo "RV5 SUMMARY: $PASS/$TOTAL PASS, $FAIL FAIL"
echo "============================================"
if [ $FAIL -eq 0 ]; then
  echo "ALL CHECKS PASSED ✅"
else
  echo "SOME CHECKS FAILED ❌"
fi
