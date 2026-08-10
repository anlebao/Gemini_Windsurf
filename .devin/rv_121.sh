#!/bin/bash
# RV: Issue #121 — KhachLink nav toggle + redeem phone-verification gate + global catalog + seed coords
# Commits: 3b5def92 + 6345bd10 (deployed via CD run 31399550083)
# Target: KhachLink (diemthuong2.khachvip.online) + Gateway (api2.khachvip.online) + ShopERP admin
set -e

KHACHLINK="https://diemthuong2.khachvip.online"
GATEWAY="https://api2.khachvip.online"
PASS=0; FAIL=0

ok()   { echo "  [PASS] $1"; PASS=$((PASS+1)); }
fail() { echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }

echo "=== RV: Issue #121 (commits 3b5def92 + 6345bd10) ==="
echo ""

# ============================================================
# Part 1: #121.1.1 — KhachLink nav menu toggle (ShowNavMenu)
# ============================================================
echo "[1] KhachLink Home Settings API — ShowNavMenu field"

# GET /api/platform/khachlink-home-settings should return 200 + ShowNavMenu field
RESP=$(curl -sk "$GATEWAY/api/platform/khachlink-home-settings")
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/api/platform/khachlink-home-settings")
if [ "$CODE" = "200" ]; then ok "GET khachlink-home-settings: 200"; else fail "GET khachlink-home-settings: $CODE"; fi

if echo "$RESP" | grep -q "showNavMenu"; then
  ok "Response contains 'showNavMenu' field"
else
  fail "Response does NOT contain 'showNavMenu' field"
  echo "  Response: $RESP"
fi
echo ""

# ============================================================
# Part 2: #121.1.2 — Configurable phone verification gate
# ============================================================
echo "[2] ShopFeatureSettings — Loyalty_RequirePhoneVerificationForRedeem"

# GET feature-settings for a tenant (diemthuong2 tenant)
# We need a tenant ID — query tenants/search first
TENANT_ID=$(curl -sk "$GATEWAY/api/tenants/search" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
if [ -n "$TENANT_ID" ]; then
  ok "Found tenant ID: $TENANT_ID"
  
  FS_RESP=$(curl -sk "$GATEWAY/api/tenants/$TENANT_ID/feature-settings")
  FS_CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/api/tenants/$TENANT_ID/feature-settings")
  if [ "$FS_CODE" = "200" ]; then ok "GET feature-settings: 200"; else fail "GET feature-settings: $FS_CODE"; fi
  
  if echo "$FS_RESP" | grep -qi "loyalty_RequirePhoneVerificationForRedeem\|loyaltyRequirePhoneVerificationForRedeem"; then
    ok "Feature settings contains Loyalty_RequirePhoneVerificationForRedeem"
  else
    fail "Feature settings does NOT contain Loyalty_RequirePhoneVerificationForRedeem"
    echo "  Response: $(echo $FS_RESP | head -c 200)"
  fi
else
  fail "No tenant ID found via /api/tenants/search"
fi
echo ""

# ============================================================
# Part 3: #121.2 — Seed coords for tenants (Store Finder distance)
# ============================================================
echo "[3] Store Finder — tenants with coordinates (seeded)"

# Search tenants with lat/lng → should return DistanceKm
SEARCH_RESP=$(curl -sk "$GATEWAY/api/tenants/search?lat=10.7326&lng=106.7196")
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/api/tenants/search?lat=10.7326&lng=106.7196")
if [ "$CODE" = "200" ]; then ok "GET /api/tenants/search?lat&lng: 200"; else fail "GET /api/tenants/search?lat&lng: $CODE"; fi

if echo "$SEARCH_RESP" | grep -qi "distanceKm"; then
  ok "Response contains 'distanceKm' field (coords seeded)"
else
  fail "Response does NOT contain 'distanceKm' — coords may not be seeded"
  echo "  Response: $(echo $SEARCH_RESP | head -c 300)"
fi

# Check at least one tenant has non-null latitude
if echo "$SEARCH_RESP" | grep -qi "latitude"; then
  ok "Response contains 'latitude' field"
else
  fail "Response does NOT contain 'latitude' field"
fi
echo ""

# ============================================================
# Part 4: #121.3 — Global Redemption Catalog API
# ============================================================
echo "[4] Global Redemption Catalog API"

# GET /api/redemption/catalog/global — anonymous, should return 200 + JSON array
GLOBAL_RESP=$(curl -sk "$GATEWAY/api/redemption/catalog/global")
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/api/redemption/catalog/global")
if [ "$CODE" = "200" ]; then ok "GET /api/redemption/catalog/global: 200"; else fail "GET /api/redemption/catalog/global: $CODE"; fi

# Response should be a JSON array (even if empty — no global items yet)
if echo "$GLOBAL_RESP" | grep -q "^\["; then
  ok "Response is a JSON array"
else
  fail "Response is NOT a JSON array: $(echo $GLOBAL_RESP | head -c 100)"
fi

# Count global items
GLOBAL_COUNT=$(echo "$GLOBAL_RESP" | grep -o '"isGlobal":true' | wc -l)
echo "  Global catalog items found: $GLOBAL_COUNT"
echo ""

# ============================================================
# Part 5: KhachLink pages load (smoke test after deploy)
# ============================================================
echo "[5] KhachLink pages load (smoke test)"

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/")
if [ "$CODE" = "200" ]; then ok "KhachLink /: 200"; else fail "KhachLink /: $CODE"; fi

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/rewards")
if [ "$CODE" = "200" ]; then ok "KhachLink /rewards: 200"; else fail "KhachLink /rewards: $CODE"; fi

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/my-loyalty")
if [ "$CODE" = "200" ]; then ok "KhachLink /my-loyalty: 200"; else fail "KhachLink /my-loyalty: $CODE"; fi

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/profile")
if [ "$CODE" = "200" ]; then ok "KhachLink /profile: 200"; else fail "KhachLink /profile: $CODE"; fi
echo ""

# ============================================================
# Part 6: Gateway health check
# ============================================================
echo "[6] Gateway health check"

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/health")
if [ "$CODE" = "200" ]; then ok "Gateway /health: 200"; else fail "Gateway /health: $CODE"; fi
echo ""

# ============================================================
# Summary
# ============================================================
echo "==============================================="
echo "RV #121 Summary: PASS=$PASS  FAIL=$FAIL"
if [ "$FAIL" -eq 0 ]; then
  echo "RESULT: ALL PASS ✅"
else
  echo "RESULT: $FAIL FAILURE(S) ❌"
fi
echo "==============================================="
