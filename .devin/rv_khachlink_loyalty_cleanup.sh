#!/bin/bash
# RV: KhachLink /my-loyalty cleanup + Store Finder relevance + distance display
# Commit: 0fcb48ca (deployed via CD run 31376845633)
# Target VPS: diemthuong2.khachvip.online (KhachLink)
set -e

# diemthuong2 = KhachLink tenant on ShopERP VPS
KHACHLINK="https://diemthuong2.khachvip.online"
GATEWAY="https://api.khachvip.online"
PASS=0; FAIL=0

ok()   { echo "  [PASS] $1"; PASS=$((PASS+1)); }
fail() { echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }

echo "=== RV: KhachLink /my-loyalty + Store Finder (commit 0fcb48ca) ==="
echo ""

# --- 1. KhachLink pages load ---
echo "[1] KhachLink pages load"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/")
if [ "$CODE" = "200" ]; then ok "KhachLink /: 200"; else fail "KhachLink /: $CODE"; fi

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/my-loyalty")
if [ "$CODE" = "200" ]; then ok "KhachLink /my-loyalty: 200"; else fail "KhachLink /my-loyalty: $CODE"; fi

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/profile")
if [ "$CODE" = "200" ]; then ok "KhachLink /profile: 200"; else fail "KhachLink /profile: $CODE"; fi
echo ""

# --- 2. /my-loyalty: demo sections removed, only History kept ---
echo "[2] /my-loyalty content checks"
HTML=$(curl -sk "$KHACHLINK/my-loyalty")

# "Đổi điểm thưởng" section should NOT appear (was demo, removed)
if echo "$HTML" | grep -q "Đổi điểm thưởng"; then
  fail "'Đổi điểm thưởng' section still visible (should be hidden)"
else
  ok "'Đổi điểm thưởng' section hidden"
fi

# "Quyền lợi theo hạng" should NOT appear (was demo, removed)
if echo "$HTML" | grep -q "Quyền lợi theo hạng"; then
  fail "'Quyền lợi theo hạng' section still visible (should be hidden)"
else
  ok "'Quyền lợi theo hạng' section hidden"
fi

# "Lịch sử tích điểm" should appear (PageTitle + History heading)
if echo "$HTML" | grep -q "Lịch sử tích điểm"; then
  ok "'Lịch sử tích điểm' text present"
else
  fail "'Lịch sử tích điểm' text NOT found"
fi
echo ""

# --- 3. NavMenu: "Lịch sử tích điểm" label + bi-clock-history icon ---
echo "[3] NavMenu label + icon"
NAV_HTML=$(curl -sk "$KHACHLINK/")

if echo "$NAV_HTML" | grep -q "Lịch sử tích điểm"; then
  ok "NavMenu shows 'Lịch sử tích điểm'"
else
  fail "NavMenu does NOT show 'Lịch sử tích điểm'"
fi

if echo "$NAV_HTML" | grep -q "bi-clock-history"; then
  ok "NavMenu uses bi-clock-history icon"
else
  # Blazor WASM may render icons differently — check DLL strings instead
  echo "  (icon check via HTML inconclusive — WASM renders client-side)"
fi

# Old label "Điểm thưởng" should NOT appear in nav context
# (Note: "Điểm thưởng" may still appear in other contexts like checkout banner)
# We check the specific nav link text
if echo "$NAV_HTML" | grep -q ">Điểm thưởng<"; then
  fail "Old nav label 'Điểm thưởng' still present"
else
  ok "Old nav label 'Điểm thưởng' removed from nav"
fi
echo ""

# --- 4. Home.razor: "Tìm quanh đây" title ---
echo "[4] Home Store Finder title"
if echo "$NAV_HTML" | grep -q "Tìm quanh đây"; then
  ok "Home shows 'Tìm quanh đây' title"
else
  # WASM renders client-side — may not be in initial HTML
  echo "  (title rendered client-side via WASM — check DLL instead)"
fi

# Old title should NOT appear
if echo "$NAV_HTML" | grep -q "Tìm cửa hàng gần bạn"; then
  fail "Old title 'Tìm cửa hàng gần bạn' still present"
else
  ok "Old title 'Tìm cửa hàng gần bạn' not in initial HTML"
fi
echo ""

# --- 5. Store Search API: relevance + FeaturedProduct match ---
echo "[5] Store Search API (Gateway)"

# Empty query → 200 + JSON array
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/api/tenants/search")
if [ "$CODE" = "200" ]; then ok "GET /api/tenants/search (no query): 200"; else fail "GET /api/tenants/search: $CODE"; fi

# With keyword → 200
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/api/tenants/search?name=cafe")
if [ "$CODE" = "200" ]; then ok "GET /api/tenants/search?name=cafe: 200"; else fail "GET /api/tenants/search?name=cafe: $CODE"; fi

# With lat/lng → 200 + DistanceKm field in response
SEARCH_RESP=$(curl -sk "$GATEWAY/api/tenants/search?name=&lat=10.7326&lng=106.7196")
CODE=$(echo "$SEARCH_RESP" | head -c 1)
if [ "$CODE" = "[" ]; then
  ok "Search with lat/lng returns JSON array"
else
  fail "Search with lat/lng does not return JSON array"
fi

# Check DistanceKm field exists in response
if echo "$SEARCH_RESP" | grep -q "distanceKm"; then
  ok "Search response contains 'distanceKm' field"
else
  fail "Search response missing 'distanceKm' field"
fi
echo ""

# --- 6. Nearby API: DistanceKm field ---
echo "[6] Nearby API (Gateway)"
NEARBY_RESP=$(curl -sk "$GATEWAY/api/tenants/nearby?lat=10.7326&lng=106.7196&radiusKm=50")
CODE=$(echo "$NEARBY_RESP" | head -c 1)
if [ "$CODE" = "[" ]; then
  ok "GET /api/tenants/nearby returns JSON array"
else
  fail "GET /api/tenants/nearby does not return JSON array"
fi

if echo "$NEARBY_RESP" | grep -q "distanceKm"; then
  ok "Nearby response contains 'distanceKm' field"
else
  fail "Nearby response missing 'distanceKm' field"
fi
echo ""

# --- 7. DLL freshness (KhachLink WASM) ---
echo "[7] KhachLink WASM DLL freshness"
DLL_TIME=$(docker exec vanan-khachlink stat -c '%Y' /app/wwwroot/_framework/VanAn.KhachLink.dll 2>/dev/null || echo 0)
NOW=$(date +%s)
AGE=$(( (NOW - DLL_TIME) / 60 ))
if [ "$AGE" -lt 60 ]; then ok "VanAn.KhachLink.dll fresh (${AGE} min ago)"; else fail "VanAn.KhachLink.dll stale (${AGE} min ago)"; fi
echo ""

# --- 8. DLL strings: verify new labels compiled into WASM ---
echo "[8] WASM DLL string checks"
STRINGS=$(docker exec vanan-khachlink strings /app/wwwroot/_framework/VanAn.KhachLink.dll 2>/dev/null)

if echo "$STRINGS" | grep -q "Lịch sử tích điểm"; then
  ok "DLL contains 'Lịch sử tích điểm'"
else
  fail "DLL missing 'Lịch sử tích điểm'"
fi

if echo "$STRINGS" | grep -q "Tìm quanh đây"; then
  ok "DLL contains 'Tìm quanh đây'"
else
  fail "DLL missing 'Tìm quanh đây'"
fi

if echo "$STRINGS" | grep -q "bi-clock-history"; then
  ok "DLL contains 'bi-clock-history' icon class"
else
  fail "DLL missing 'bi-clock-history' icon class"
fi

# Old strings should be gone or reduced
if echo "$STRINGS" | grep -q "Đổi điểm thưởng"; then
  fail "DLL still contains 'Đổi điểm thưởng' (should be removed)"
else
  ok "DLL no longer contains 'Đổi điểm thưởng'"
fi
echo ""

# --- Summary ---
echo "========================================"
echo "  RV SUMMARY: $PASS PASS, $FAIL FAIL"
if [ "$FAIL" -eq 0 ]; then
  echo "  ✅ ALL PASS — KhachLink fixes verified"
else
  echo "  ❌ $FAIL failures — investigate"
fi
echo "========================================"
