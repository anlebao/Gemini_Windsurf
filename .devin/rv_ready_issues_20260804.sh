#!/bin/bash
# RV: Ready issues fixed today (2026-08-04) — HTTP + DB based
# Commits: c9ac98cc (#98), a8b5510f (#99), e1121579 (#93), 76d61670 (#100)
set -e

GATEWAY="https://api.khachvip.online"
SHOPERP="https://khachvip.online"
KHACHLINK="https://diemthuong.khachvip.online"
PASS=0; FAIL=0; WARN=0

ok()   { echo "  [PASS] $1"; PASS=$((PASS+1)); }
fail() { echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }
warn() { echo "  [WARN] $1"; WARN=$((WARN+1)); }

echo "=== RV: Ready Issues (2026-08-04) — HTTP + DB ==="
echo ""

# --- 0. Containers healthy ---
echo "[0] Containers healthy"
HEALTH=$(docker ps --format "{{.Names}}:{{.Status}}" 2>/dev/null | grep -c "Up" || echo 0)
if [ "$HEALTH" -ge 7 ]; then ok "$HEALTH containers Up"; else fail "$HEALTH containers Up"; fi
echo ""

# --- 1. DLL freshness ---
echo "[1] DLL freshness (deployed < 3h ago)"
for APP_PATH in "vanan-gateway:/app/VanAn.Gateway.dll" "vanan-shoperp:/app/VanAn.ShopERP.dll"; do
  APP="${APP_PATH%%:*}"; PATH_="${APP_PATH##*:}"
  DLL_TIME=$(docker exec "$APP" stat -c '%Y' "$PATH_" 2>/dev/null | tr -d '\r\n ' || echo 0)
  NOW=$(date +%s); AGE=$(( (NOW - DLL_TIME) / 60 ))
  if [ "$AGE" -lt 180 ]; then ok "$APP DLL fresh (${AGE} min ago)"; else warn "$APP DLL stale (${AGE} min)"; fi
done
# KhachLink WASM
WASM_TIME=$(docker exec vanan-khachlink stat -c '%Y' /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null | tr -d '\r\n ' || echo 0)
NOW=$(date +%s); AGE=$(( (NOW - WASM_TIME) / 60 ))
if [ "$AGE" -lt 180 ]; then ok "KhachLink WASM fresh (${AGE} min ago)"; else warn "KhachLink WASM stale (${AGE} min)"; fi
echo ""

# --- 2. #98: OrderNotificationService → LocationHub (DB + Gateway DLL) ---
echo "[2] #98: Order status sync"
# Gateway DLL contains OrderStatusUpdated + LocationHub (grep binary)
GW_MATCHES=$(docker exec vanan-gateway grep -ac "OrderStatusUpdated" /app/VanAn.Gateway.dll 2>/dev/null | tr -d '\r\n ' || echo 0)
if [ "$GW_MATCHES" -ge 1 ]; then ok "OrderStatusUpdated in Gateway DLL ($GW_MATCHES)"; else fail "OrderStatusUpdated NOT in Gateway DLL"; fi

LOC_MATCHES=$(docker exec vanan-gateway grep -ac "LocationHub" /app/VanAn.Gateway.dll 2>/dev/null | tr -d '\r\n ' || echo 0)
if [ "$LOC_MATCHES" -ge 2 ]; then ok "LocationHub refs in Gateway DLL ($LOC_MATCHES)"; else fail "LocationHub NOT enough ($LOC_MATCHES)"; fi

# KhachLink WASM contains OrderStatusUpdated handler
KL_MATCHES=$(docker exec vanan-khachlink grep -ac "OrderStatusUpdated" /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null | tr -d '\r\n ' || echo 0)
if [ "$KL_MATCHES" -ge 1 ]; then ok "OrderStatusUpdated in KhachLink WASM ($KL_MATCHES)"; else warn "OrderStatusUpdated NOT in WASM (may be inlined)"; fi
echo ""

# --- 3. #99: Redemption error handling ---
echo "[3] #99: Redemption error handling"
# ShopERP DLL contains error message
ERR_MATCHES=$(docker exec vanan-shoperp grep -ac "Tài khoản chưa xác minh" /app/VanAn.ShopERP.dll 2>/dev/null | tr -d '\r\n ' || echo 0)
if [ "$ERR_MATCHES" -ge 1 ]; then ok "IdentityLevel error msg in ShopERP DLL"; else fail "IdentityLevel error msg NOT found"; fi

# Test redemption endpoint with invalid token — should NOT return 500
CODE=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "$GATEWAY/api/redemption/redeem" \
  -H "Content-Type: application/json" \
  -H "X-Customer-Token: invalid-token-test" \
  -d '{"CatalogItemId":"00000000-0000-0000-0000-000000000000"}')
if [ "$CODE" = "401" ] || [ "$CODE" = "400" ]; then ok "Redeem invalid token: $CODE (not 500)"; else fail "Redeem invalid token: $CODE (expected 401/400)"; fi

# Test with valid token format but fake catalog item — should return 400 with error message, not 500
CODE2=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "$GATEWAY/api/redemption/redeem" \
  -H "Content-Type: application/json" \
  -d '{"CatalogItemId":"00000000-0000-0000-0000-000000000000"}')
if [ "$CODE2" = "401" ] || [ "$CODE2" = "400" ]; then ok "Redeem no token: $CODE2 (not 500)"; else fail "Redeem no token: $CODE2 (expected 401/400)"; fi
echo ""

# --- 4. #93: Style customization ---
echo "[4] #93: Style customization"
# DB migration applied
NAV_COL=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT COUNT(*) FROM information_schema.columns WHERE table_name='Tenants' AND column_name='Settings_NavColor';" 2>/dev/null | tr -d '\r\n ' || echo 0)
if [ "$NAV_COL" -ge 1 ]; then ok "Settings_NavColor column in DB"; else fail "Settings_NavColor NOT in DB"; fi

HDR_COL=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT COUNT(*) FROM information_schema.columns WHERE table_name='Tenants' AND column_name='Settings_HeaderColor';" 2>/dev/null | tr -d '\r\n ' || echo 0)
if [ "$HDR_COL" -ge 1 ]; then ok "Settings_HeaderColor column in DB"; else fail "Settings_HeaderColor NOT in DB"; fi

# Gateway DLL contains NavColor
NAV_DLL=$(docker exec vanan-gateway grep -ac "NavColor" /app/VanAn.Gateway.dll 2>/dev/null | tr -d '\r\n ' || echo 0)
if [ "$NAV_DLL" -ge 1 ]; then ok "NavColor in Gateway DLL ($NAV_DLL)"; else fail "NavColor NOT in Gateway DLL"; fi

# store-info endpoint returns style fields
TENANT_ID=$(curl -sk "$GATEWAY/api/tenants/search" 2>/dev/null | python3 -c "import sys,json; d=json.load(sys.stdin); print(d[0]['id'] if d else '')" 2>/dev/null | tr -d '\r\n ' || echo "")
if [ -n "$TENANT_ID" ]; then
  BODY=$(curl -sk "$GATEWAY/api/tenants/$TENANT_ID/store-info" 2>/dev/null)
  echo "$BODY" | grep -q '"navColor"' && ok "store-info returns navColor" || fail "store-info missing navColor"
  echo "$BODY" | grep -q '"headerColor"' && ok "store-info returns headerColor" || fail "store-info missing headerColor"
  echo "$BODY" | grep -q '"footerColor"' && ok "store-info returns footerColor" || fail "store-info missing footerColor"
else
  warn "No tenants found for store-info test"
fi
echo ""

# --- 5. #100: Home section toggles ---
echo "[5] #100: Home section toggles"
# DB migration applied
HOME_COL=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT COUNT(*) FROM information_schema.columns WHERE table_name='ShopFeatureSettings' AND column_name='Home_CampaignSection_Enabled';" 2>/dev/null | tr -d '\r\n ' || echo 0)
if [ "$HOME_COL" -ge 1 ]; then ok "Home_CampaignSection_Enabled column in DB"; else fail "Home_CampaignSection_Enabled NOT in DB"; fi

STORE_COL=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT COUNT(*) FROM information_schema.columns WHERE table_name='ShopFeatureSettings' AND column_name='Home_StoreSection_Enabled';" 2>/dev/null | tr -d '\r\n ' || echo 0)
if [ "$STORE_COL" -ge 1 ]; then ok "Home_StoreSection_Enabled column in DB"; else fail "Home_StoreSection_Enabled NOT in DB"; fi

FEAT_COL=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT COUNT(*) FROM information_schema.columns WHERE table_name='ShopFeatureSettings' AND column_name='Home_FeaturedSection_Enabled';" 2>/dev/null | tr -d '\r\n ' || echo 0)
if [ "$FEAT_COL" -ge 1 ]; then ok "Home_FeaturedSection_Enabled column in DB"; else fail "Home_FeaturedSection_Enabled NOT in DB"; fi

SOCIAL_COL=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT COUNT(*) FROM information_schema.columns WHERE table_name='ShopFeatureSettings' AND column_name='Home_SocialHub_Enabled';" 2>/dev/null | tr -d '\r\n ' || echo 0)
if [ "$SOCIAL_COL" -ge 1 ]; then ok "Home_SocialHub_Enabled column in DB"; else fail "Home_SocialHub_Enabled NOT in DB"; fi

# ShopERP DLL contains admin UI card text
CARD_MATCHES=$(docker exec vanan-shoperp grep -ac "Hiển thị trang chủ KhachLink" /app/VanAn.ShopERP.dll 2>/dev/null | tr -d '\r\n ' || echo 0)
if [ "$CARD_MATCHES" -ge 1 ]; then ok "Home sections card in ShopERP DLL"; else fail "Home sections card NOT in ShopERP DLL"; fi

# ShopERP DLL contains _isSaving save notification
SAVE_MATCHES=$(docker exec vanan-shoperp grep -ac "Đã lưu cấu hình thành công" /app/VanAn.ShopERP.dll 2>/dev/null | tr -d '\r\n ' || echo 0)
if [ "$SAVE_MATCHES" -ge 1 ]; then ok "Save success message in ShopERP DLL"; else fail "Save success message NOT in ShopERP DLL"; fi
echo ""

# --- 6. Endpoint smoke tests ---
echo "[6] Endpoint smoke tests"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/health")
[ "$CODE" = "200" ] && ok "Gateway /health: 200" || fail "Gateway /health: $CODE"

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/")
[ "$CODE" = "200" ] && ok "KhachLink /: 200" || fail "KhachLink /: $CODE"

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/rewards")
[ "$CODE" = "200" ] && ok "KhachLink /rewards: 200" || fail "KhachLink /rewards: $CODE"

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/cart")
[ "$CODE" = "200" ] && ok "KhachLink /cart: 200" || fail "KhachLink /cart: $CODE"

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/")
[ "$CODE" = "200" ] || [ "$CODE" = "302" ] && ok "ShopERP /: $CODE" || fail "ShopERP /: $CODE"

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/api/tenants/search")
[ "$CODE" = "200" ] && ok "GET /api/tenants/search: 200" || fail "GET /api/tenants/search: $CODE"
echo ""

# --- Summary ---
echo "========================================"
echo "  RV SUMMARY: $PASS PASS, $WARN WARN, $FAIL FAIL"
if [ "$FAIL" -eq 0 ]; then
  echo "  ALL PASS — Ready issues verified on VPS"
elif [ "$FAIL" -le 2 ]; then
  echo "  $FAIL failures — check details above"
else
  echo "  $FAIL failures — investigate"
fi
echo "========================================"
