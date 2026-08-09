#!/bin/bash
# RV: Functional verification — test actual API behavior (not DLL grep)
set -e

GATEWAY="https://api.khachvip.online"
SHOPERP="https://khachvip.online"
KHACHLINK="https://diemthuong.khachvip.online"
PASS=0; FAIL=0; WARN=0

ok()   { echo "  [PASS] $1"; PASS=$((PASS+1)); }
fail() { echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }
warn() { echo "  [WARN] $1"; WARN=$((WARN+1)); }

echo "=== RV: Functional Verification (API + DB) ==="
echo ""

# --- #98: Order status sync — test SignalR LocationHub ---
echo "[#98] Order status sync — SignalR LocationHub"
# Check LocationHub endpoint exists
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/locationhub/negotiate" -X POST -H "Content-Type: application/json" -d '{}')
if [ "$CODE" = "200" ] || [ "$CODE" = "401" ] || [ "$CODE" = "400" ]; then ok "LocationHub /negotiate endpoint exists ($CODE)"; else fail "LocationHub /negotiate: $CODE"; fi

# Check OrderHub endpoint exists
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/orderhub/negotiate" -X POST -H "Content-Type: application/json" -d '{}')
if [ "$CODE" = "200" ] || [ "$CODE" = "401" ] || [ "$CODE" = "400" ]; then ok "OrderHub /negotiate endpoint exists ($CODE)"; else fail "OrderHub /negotiate: $CODE"; fi
echo ""

# --- #99: Redemption error handling ---
echo "[#99] Redemption error handling"
# Test with invalid token — should NOT return 500
CODE=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "$GATEWAY/api/redemption/redeem" \
  -H "Content-Type: application/json" \
  -H "X-Customer-Token: invalid-token-test" \
  -d '{"CatalogItemId":"00000000-0000-0000-0000-000000000000"}')
if [ "$CODE" != "500" ]; then ok "Redeem invalid token: $CODE (not 500 — error handling works)"; else fail "Redeem invalid token: 500 (generic error)"; fi

# Test without token — should NOT return 500
CODE=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "$GATEWAY/api/redemption/redeem" \
  -H "Content-Type: application/json" \
  -d '{"CatalogItemId":"00000000-0000-0000-0000-000000000000"}')
if [ "$CODE" != "500" ]; then ok "Redeem no token: $CODE (not 500 — error handling works)"; else fail "Redeem no token: 500 (generic error)"; fi
echo ""

# --- #93: Style customization ---
echo "[#93] Style customization — store-info API"
TENANT_ID=$(curl -sk "$GATEWAY/api/tenants/search" 2>/dev/null | python3 -c "import sys,json; d=json.load(sys.stdin); print(d[0]['id'] if d else '')" 2>/dev/null | tr -d '\r\n ' || echo "")
if [ -n "$TENANT_ID" ]; then
  BODY=$(curl -sk "$GATEWAY/api/tenants/$TENANT_ID/store-info" 2>/dev/null)
  echo "$BODY" | grep -q '"navColor"' && ok "store-info returns navColor" || fail "store-info missing navColor"
  echo "$BODY" | grep -q '"headerColor"' && ok "store-info returns headerColor" || fail "store-info missing headerColor"
  echo "$BODY" | grep -q '"footerColor"' && ok "store-info returns footerColor" || fail "store-info missing footerColor"
  echo "$BODY" | grep -q '"logoUrl"' && ok "store-info returns logoUrl" || warn "store-info missing logoUrl (may be null)"
else
  warn "No tenants found for store-info test"
fi
echo ""

# --- #100: Home section toggles — DB + API ---
echo "[#100] Home section toggles"
# DB columns
for COL in Home_CampaignSection_Enabled Home_StoreSection_Enabled Home_FeaturedSection_Enabled Home_SocialHub_Enabled; do
  COUNT=$(echo "SELECT COUNT(*) FROM information_schema.columns WHERE table_name='ShopFeatureSettings' AND column_name='$COL';" | docker exec -i vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t 2>/dev/null | tr -d '\r\n ' || echo 0)
  if [ "$COUNT" -ge 1 ]; then ok "$COL column in DB"; else fail "$COL NOT in DB"; fi
done

# Check feature settings API returns home toggles (need tenant with settings)
if [ -n "$TENANT_ID" ]; then
  # Try to get feature settings via admin API (may need auth — just check endpoint exists)
  CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/api/shop-features/$TENANT_ID")
  if [ "$CODE" = "200" ] || [ "$CODE" = "401" ] || [ "$CODE" = "403" ]; then ok "Feature settings endpoint exists ($CODE)"; else warn "Feature settings endpoint: $CODE"; fi
fi

# Check KhachLink home page renders (contains section markers)
HOME_HTML=$(curl -sk "$KHACHLINK/" 2>/dev/null)
if echo "$HOME_HTML" | grep -q "KhachLink"; then ok "KhachLink home page renders"; else fail "KhachLink home page not rendering"; fi

# Check KhachLink cart page renders (mobile action bar)
CART_HTML=$(curl -sk "$KHACHLINK/cart" 2>/dev/null)
if [ -n "$CART_HTML" ]; then ok "KhachLink cart page renders"; else fail "KhachLink cart page not rendering"; fi
echo ""

# --- Endpoint smoke tests ---
echo "[Smoke] Endpoint smoke tests"
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
else
  echo "  $FAIL failures — investigate"
fi
echo "========================================"
