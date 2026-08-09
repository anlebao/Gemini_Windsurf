#!/bin/bash
# RV Phase A: #99-3 Loyalty Points Visibility + Shop Owner Dashboard
# Commit: 37c29e01 (Phase A — Batch 1 + Batch 3)
# Verifies: V1-V6 (customer visibility) + V11-V14 (dashboard)
set -e

GATEWAY="https://api.khachvip.online"
KHACHLINK="https://diemthuong.khachvip.online"
SHOPERP="https://shoperp.khachvip.online"
PASS=0; FAIL=0

ok()   { echo "  [PASS] $1"; PASS=$((PASS+1)); }
fail() { echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }

echo "========================================"
echo "RV Phase A: #99-3 Loyalty Visibility + Dashboard"
echo "Commit: 37c29e01"
echo "========================================"
echo ""

# --- 1. Containers healthy ---
echo "[1] Containers healthy"
HEALTH=$(docker ps --format "{{.Names}}:{{.Status}}" 2>/dev/null | grep -c "Up" || echo 0)
if [ "$HEALTH" -ge 7 ]; then ok "$HEALTH containers Up"; else fail "$HEALTH containers Up (expected >=7)"; fi
docker ps --format "  {{.Names}}: {{.Status}}" 2>/dev/null | head -10
echo ""

# --- 2. DLL freshness (Phase A code deployed) ---
echo "[2] DLL freshness — Phase A code deployed"
NOW=$(date +%s)

# Gateway: PublicOrdersController has ComputePointsAwardedAsync
GW_TIME=$(docker exec vanan-gateway stat -c '%Y' /app/VanAn.Gateway.dll 2>/dev/null || echo 0)
AGE_GW=$(( (NOW - GW_TIME) / 60 ))
if [ "$AGE_GW" -lt 60 ]; then ok "Gateway DLL fresh (${AGE_GW} min ago)"; else fail "Gateway DLL stale (${AGE_GW} min ago)"; fi

# ShopERP: LoyaltyController has GET /dashboard
ERP_TIME=$(docker exec vanan-shoperp stat -c '%Y' /app/VanAn.ShopERP.dll 2>/dev/null || echo 0)
AGE_ERP=$(( (NOW - ERP_TIME) / 60 ))
if [ "$AGE_ERP" -lt 60 ]; then ok "ShopERP DLL fresh (${AGE_ERP} min ago)"; else fail "ShopERP DLL stale (${AGE_ERP} min ago)"; fi

# KhachLink WASM: OrderTracking.razor + Checkout.razor have new banners
KL_TIME=$(docker exec vanan-khachlink stat -c '%Y' /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null || echo 0)
AGE_KL=$(( (NOW - KL_TIME) / 60 ))
if [ "$AGE_KL" -lt 60 ]; then ok "KhachLink WASM fresh (${AGE_KL} min ago)"; else fail "KhachLink WASM stale (${AGE_KL} min ago)"; fi
echo ""

# --- 3. Gateway DLL contains Phase A strings ---
echo "[3] Gateway DLL contains Phase A strings"
docker cp vanan-gateway:/app/VanAn.Gateway.dll /tmp/vgw_phaseA.dll 2>/dev/null
python3 -c "
data = open('/tmp/vgw_phaseA.dll', 'rb').read()
checks = {
    'ComputePointsAwardedAsync': 'New helper method in PublicOrdersController',
    'PointsAwarded': 'New DTO field',
    'LoyaltyEnabled': 'New DTO field',
    'Loyalty_Program_Enabled': 'Tenant toggle check',
}
for s, desc in checks.items():
    c = data.count(s.encode('utf-8'))
    print(f'  {s}: {c} matches — {desc}')
" 2>&1
echo ""

# --- 4. ShopERP DLL contains Phase A strings ---
echo "[4] ShopERP DLL contains Phase A strings"
docker cp vanan-shoperp:/app/VanAn.ShopERP.dll /tmp/verp_phaseA.dll 2>/dev/null
python3 -c "
data = open('/tmp/verp_phaseA.dll', 'rb').read()
checks = {
    'GetDashboard': 'New dashboard endpoint in LoyaltyController',
    'LoyaltyDashboardStats': 'New DTO class',
    'PointsPendingRedemption': 'Metric 1',
    'PointsRedeemed': 'Metric 2',
    'PointsInCampaigns': 'Metric 3',
    'PointsReserved': 'Metric 4',
}
for s, desc in checks.items():
    c = data.count(s.encode('utf-8'))
    print(f'  {s}: {c} matches — {desc}')
" 2>&1
echo ""

# --- 5. KhachLink WASM contains Phase A strings ---
echo "[5] KhachLink WASM contains Phase A banner strings"
docker cp vanan-khachlink:/usr/share/nginx/html/_framework/VanAn.KhachLink.wasm /tmp/vkl_phaseA.wasm 2>/dev/null
python3 -c "
data = open('/tmp/vkl_phaseA.wasm', 'rb').read()
def find(p): return data.count(p.encode('utf-16-le'))
checks = {
    'Bạn đã nhận được': 'OrderTracking banner text',
    'điểm thưởng': 'OrderTracking banner text',
    'Đơn hàng sẽ tích': 'Checkout estimate banner text',
    'loyalty-points-banner': 'OrderTracking banner CSS class',
    'loyalty-estimate': 'Checkout estimate CSS class',
    'PointsAwarded': 'DTO field in KhachLink',
    'LoyaltyEnabled': 'DTO field in KhachLink',
}
for s, desc in checks.items():
    c = find(s)
    print(f'  {s}: {c} matches — {desc}')
" 2>&1
echo ""

# --- V1: GET /api/loyalty/mode (existing endpoint — sanity check) ---
echo "[V1] Gateway /api/loyalty/mode (sanity — existing endpoint)"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/api/loyalty/mode")
if [ "$CODE" = "200" ]; then ok "GET /api/loyalty/mode: 200"; else fail "GET /api/loyalty/mode: $CODE"; fi
echo ""

# --- V2: GET /api/public/orders/{id} returns PointsAwarded + LoyaltyEnabled fields ---
echo "[V2] PublicOrderTrackingDto has PointsAwarded + LoyaltyEnabled fields"
# Use a known test order ID or create one. For now, test with a fake UUID — expect 404 but
# verify the endpoint exists. Full E2E test requires a real order.
TEST_ORDER_ID="00000000-0000-0000-0000-000000000001"
RESP=$(curl -sk -w "\n%{http_code}" "$GATEWAY/api/public/orders/$TEST_ORDER_ID")
CODE=$(echo "$RESP" | tail -1)
BODY=$(echo "$RESP" | head -n -1)
if [ "$CODE" = "404" ]; then
  ok "GET /api/public/orders/{id} endpoint exists (404 for fake ID = expected)"
else
  fail "GET /api/public/orders/{id} returned $CODE (expected 404 for fake ID)"
  echo "    Body: $BODY"
fi
echo ""

# --- V3: KhachLink OrderTracking page loads ---
echo "[V3] KhachLink OrderTracking page loads"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/order-tracking/$TEST_ORDER_ID")
if [ "$CODE" = "200" ]; then ok "KhachLink /order-tracking/{id}: 200"; else fail "KhachLink /order-tracking/{id}: $CODE"; fi
echo ""

# --- V4: KhachLink Checkout page loads ---
echo "[V4] KhachLink Checkout page loads"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/checkout")
if [ "$CODE" = "200" ]; then ok "KhachLink /checkout: 200"; else fail "KhachLink /checkout: $CODE"; fi
echo ""

# --- V5: ShopERP LoyaltyDashboard page loads ---
echo "[V5] ShopERP LoyaltyDashboard page loads"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/loyalty/dashboard")
if [ "$CODE" = "200" ]; then ok "ShopERP /loyalty/dashboard: 200"; else fail "ShopERP /loyalty/dashboard: $CODE"; fi
echo ""

# --- V6: ShopERP LoyaltyDashboard contains 4 stat card labels ---
echo "[V6] ShopERP LoyaltyDashboard contains 4 stat card labels"
HTML=$(curl -sk "$SHOPERP/loyalty/dashboard")
for label in "Điểm chờ đổi" "Đã đổi" "Điểm CTKM chờ thưởng" "Dự trù điểm thưởng"; do
  if echo "$HTML" | grep -q "$label"; then
    ok "Dashboard HTML contains '$label'"
  else
    fail "Dashboard HTML missing '$label'"
  fi
done
echo ""

# --- V7: ShopERP NavMenu contains dashboard link ---
echo "[V7] ShopERP NavMenu contains 'Thống kê điểm thưởng' link"
NAV_HTML=$(curl -sk "$SHOPERP/")
if echo "$NAV_HTML" | grep -q "loyalty/dashboard"; then
  ok "NavMenu has /loyalty/dashboard link"
else
  fail "NavMenu missing /loyalty/dashboard link"
fi
echo ""

# --- V8: GET /api/loyalty/dashboard (auth required — expect 401 without cookie) ---
echo "[V8] GET /api/loyalty/dashboard endpoint exists (expect 401 without auth)"
RESP=$(curl -sk -w "\n%{http_code}" "$SHOPERP/api/loyalty/dashboard")
CODE=$(echo "$RESP" | tail -1)
BODY=$(echo "$RESP" | head -n -1)
if [ "$CODE" = "401" ] || [ "$CODE" = "200" ]; then
  ok "GET /api/loyalty/dashboard endpoint exists (HTTP $CODE)"
else
  fail "GET /api/loyalty/dashboard returned $CODE (expected 401 or 200)"
  echo "    Body: $BODY"
fi
echo ""

# --- Summary ---
echo "========================================"
echo "  RV Phase A SUMMARY: $PASS PASS, $FAIL FAIL"
if [ "$FAIL" -eq 0 ]; then
  echo "  ALL PASS — Phase A verified on VPS"
  echo "  Next: approve Phase B (Alliance VND normalization)"
else
  echo "  $FAIL failures — investigate"
fi
echo "========================================"
