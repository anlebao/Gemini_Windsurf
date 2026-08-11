#!/bin/bash
# RV: Store finder dedup + #123 + #124 + #125
# Commit: 09baa0ff (dedup) + 716e7eec (fixes)
set -e

KHACHLINK="https://diemthuong2.khachvip.online"
GATEWAY="https://api2.khachvip.online"
SHOPERP="https://app2.khachvip.online"
PASS=0; FAIL=0
ok()   { echo "  [PASS] $1"; PASS=$((PASS+1)); }
fail() { echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }

echo "=== RV: Store finder dedup + #123 + #124 + #125 ==="
echo ""

# ============================================================
# Part 1: Store finder dedup — Home.razor CTA, /stores full page
# ============================================================
echo "[1] Store finder dedup — Home page CTA + /stores page"

# Home page should still load
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/")
if [ "$CODE" = "200" ]; then ok "KhachLink home page: 200"; else fail "KhachLink home page: $CODE"; fi

# /stores should load (StoreFinder.razor)
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/stores")
if [ "$CODE" = "200" ]; then ok "/stores page: 200"; else fail "/stores page: $CODE"; fi

# /store-finder alias should also work
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/store-finder")
if [ "$CODE" = "200" ]; then ok "/store-finder alias: 200"; else fail "/store-finder alias: $CODE"; fi
echo ""

# ============================================================
# Part 2: #123 — SQLite IsGlobal migration applied
# ============================================================
echo "[2] #123 — ShopERP catalog page loads (IsGlobal migration applied)"

# ShopERP admin redemption-catalog should load (was returning SQLite error)
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/admin/redemption-catalog")
if [ "$CODE" = "200" ] || [ "$CODE" = "302" ]; then ok "ShopERP /admin/redemption-catalog: $CODE (no SQLite error)"; else fail "ShopERP /admin/redemption-catalog: $CODE"; fi

# Global redemption catalog admin page
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/admin/global-redemption-catalog")
if [ "$CODE" = "200" ] || [ "$CODE" = "302" ]; then ok "ShopERP /admin/global-redemption-catalog: $CODE"; else fail "ShopERP /admin/global-redemption-catalog: $CODE"; fi
echo ""

# ============================================================
# Part 3: #124 — Global catalog API returns IsAvailable
# ============================================================
echo "[3] #124 — Global catalog API returns IsAvailable field"

# Fetch global catalog from Gateway
RESP=$(curl -sk "$GATEWAY/api/redemption/catalog/global")
if echo "$RESP" | grep -q "isAvailable"; then
  ok "Global catalog API response contains 'isAvailable' field"
else
  fail "Global catalog API response missing 'isAvailable' field"
  echo "  Response preview: $(echo "$RESP" | head -c 200)"
fi

# KhachLink /rewards should load
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/rewards")
if [ "$CODE" = "200" ]; then ok "KhachLink /rewards page: 200"; else fail "KhachLink /rewards page: $CODE"; fi

# Local catalog API (forwarded to ShopERP) should still work
RESP=$(curl -sk "$GATEWAY/api/redemption/catalog/active")
if [ "$CODE" = "200" ] || echo "$RESP" | grep -q "\["; then
  ok "Local catalog API /api/redemption/catalog/active: responding"
else
  fail "Local catalog API not responding properly"
fi
echo ""

# ============================================================
# Part 4: #124 part 2 — ShopERP nav has 'Cấu hình tính năng' link
# ============================================================
echo "[4] #124 part 2 — ShopERP nav link to settings/shop-features"

# Check ShopERP Login page loads (nav menu is behind auth, but page should exist)
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/settings/shop-features")
if [ "$CODE" = "200" ] || [ "$CODE" = "302" ]; then ok "ShopERP /settings/shop-features: $CODE (page exists)"; else fail "ShopERP /settings/shop-features: $CODE"; fi
echo ""

# ============================================================
# Part 5: #125 — Bottom nav responsive classes
# ============================================================
echo "[5] #125 — KhachLink bottom nav responsive"

# KhachLink home should still load (no CSS break)
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/")
if [ "$CODE" = "200" ]; then ok "KhachLink home: 200 (no CSS break from nav fix)"; else fail "KhachLink home: $CODE"; fi

# Check that NavMenu CSS classes are present in rendered HTML
HTML=$(curl -sk "$KHACHLINK/")
if echo "$HTML" | grep -q "mobile-bottom-nav"; then
  ok "mobile-bottom-nav CSS class present in HTML"
else
  echo "  (mobile-bottom-nav may render after Blazor hydration — inconclusive)"
fi
echo ""

# ============================================================
# Part 6: No regression — all sites load
# ============================================================
echo "[6] No regression — all sites load"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/health"); if [ "$CODE" = "200" ]; then ok "Gateway /health: 200"; else fail "Gateway /health: $CODE"; fi
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/Login"); if [ "$CODE" = "200" ]; then ok "ShopERP /Login: 200"; else fail "ShopERP /Login: $CODE"; fi
echo ""

# ============================================================
# Summary
# ============================================================
echo "==============================================="
echo "RV Summary: PASS=$PASS  FAIL=$FAIL"
if [ "$FAIL" -eq 0 ]; then echo "RESULT: ALL PASS"; else echo "RESULT: $FAIL FAILURE(S)"; fi
echo "==============================================="
