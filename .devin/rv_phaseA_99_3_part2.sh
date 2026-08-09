#!/bin/bash
set +e
GATEWAY="https://api.khachvip.online"
KHACHLINK="https://diemthuong.khachvip.online"
SHOPERP="https://app.khachvip.online"
PASS=0; FAIL=0

ok()   { echo "  [PASS] $1"; PASS=$((PASS+1)); }
fail() { echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }

echo "[V5] ShopERP LoyaltyDashboard page loads"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/loyalty/dashboard")
if [ "$CODE" = "200" ]; then ok "ShopERP /loyalty/dashboard: 200"; else fail "ShopERP /loyalty/dashboard: $CODE"; fi
echo ""

echo "[V6] ShopERP LoyaltyDashboard contains 4 stat card labels"
HTML=$(curl -sk "$SHOPERP/loyalty/dashboard")
for label in "Điểm chờ đổi" "Đã đổi" "Điểm CTKM chờ thưởng" "Dự trù điểm thưởng"; do
  if echo "$HTML" | grep -q "$label"; then
    ok "Dashboard HTML contains '$label'"
  else
    fail "Dashboard HTML missing '$label' (likely interactive-only render — check after JS load)"
  fi
done
echo ""

echo "[V7] ShopERP NavMenu contains dashboard link"
NAV_HTML=$(curl -sk "$SHOPERP/")
if echo "$NAV_HTML" | grep -q "loyalty/dashboard"; then
  ok "NavMenu has /loyalty/dashboard link"
else
  fail "NavMenu missing /loyalty/dashboard link (likely auth-gated, prerender empty)"
fi
echo ""

echo "[V8] GET /api/loyalty/dashboard endpoint (expect 401 without auth)"
RESP=$(curl -sk -w "\n%{http_code}" "$SHOPERP/api/loyalty/dashboard")
CODE=$(echo "$RESP" | tail -1)
BODY=$(echo "$RESP" | head -n -1)
if [ "$CODE" = "401" ] || [ "$CODE" = "200" ] || [ "$CODE" = "302" ]; then
  ok "GET /api/loyalty/dashboard endpoint exists (HTTP $CODE)"
else
  fail "GET /api/loyalty/dashboard returned $CODE (expected 401/200/302)"
  echo "    Body: $BODY"
fi
echo ""

echo "========================================"
echo "  RV Phase A (V5-V8): $PASS PASS, $FAIL FAIL"
echo "========================================"
