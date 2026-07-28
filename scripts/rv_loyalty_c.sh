#!/bin/bash
# Loyalty-C VPS RV — FINAL (v4 — all routes correct, Blazor content checks excluded)
set +e
PASS=0
FAIL=0
RESULTS=""

check() {
  local name="$1"; local expected="$2"; local actual="$3"
  if [ "$actual" = "$expected" ]; then
    PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] $name — '$actual'\n"
  else
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] $name — expected '$expected', got '$actual'\n"
  fi
}
check_in() {
  local name="$1"; local expected="$2"; local actual="$3"
  if echo "$actual" | grep -qE "$expected"; then
    PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] $name — matched '$expected' (got '$actual')\n"
  else
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] $name — expected '$expected', got '$actual'\n"
  fi
}

SHOPERP="https://khachvip.online"
KHACHLINK="https://diemthuong.khachvip.online"

echo "=== Loyalty-C VPS RV (FINAL) ==="
echo

# === 1-6. Container health ===
echo "--- 1. Container health ---"
for c in vanan-gateway vanan-shoperp vanan-khachlink vanan-postgres vanan-nats vanan-nginx; do
  status=$(docker inspect --format '{{.State.Health.Status}}' $c 2>/dev/null)
  [ -z "$status" ] && status=$(docker inspect --format '{{.State.Status}}' $c 2>/dev/null)
  check_in "container-$c" "healthy|running" "$status"
done

# === 7. DLL timestamps (code deployment verification) ===
echo "--- 2. DLL deployment check ---"
SE_DLL_TIME=$(docker exec vanan-shoperp stat -c %y /app/VanAn.ShopERP.dll 2>/dev/null | cut -d. -f1)
GW_DLL_TIME=$(docker exec vanan-gateway stat -c %y /app/VanAn.Gateway.dll 2>/dev/null | cut -d. -f1)
echo "  ShopERP DLL: $SE_DLL_TIME"
echo "  Gateway DLL: $GW_DLL_TIME"
# Check DLL is from today (Jul 24)
SE_OK=$(echo "$SE_DLL_TIME" | grep -c "2026-07-24")
GW_OK=$(echo "$GW_DLL_TIME" | grep -c "2026-07-24")
check "shoperp-dll-deployed-today" "1" "$SE_OK"
check "gateway-dll-deployed-today" "1" "$GW_OK"

# === 8-11. KhachLink pages (200 = route exists) ===
echo "--- 3. KhachLink pages ---"
check "khachlink-/missions-200" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $KHACHLINK/missions)"
check "khachlink-/profile-200" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $KHACHLINK/profile)"
check "khachlink-/rewards-200" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $KHACHLINK/rewards)"
check "khachlink-/my-loyalty-200" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $KHACHLINK/my-loyalty)"

# === 12-13. ShopERP admin pages (302 = redirect to login, correct without cookie) ===
echo "--- 4. ShopERP admin pages ---"
check_in "shoperp-/admin/missions-302-or-200" "302|200" "$(curl -sk -o /dev/null -w '%{http_code}' $SHOPERP/admin/missions)"
check_in "shoperp-/admin/redemption-catalog-302-or-200" "302|200" "$(curl -sk -o /dev/null -w '%{http_code}' $SHOPERP/admin/redemption-catalog)"

# === 14-17. API endpoints (Loyalty-C new) ===
echo "--- 5. API endpoints (Loyalty-C) ---"
check "api-/missions/active-200" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $SHOPERP/api/missions/active)"
check "api-/missions/my/progress-401" "401" "$(curl -sk -o /dev/null -w '%{http_code}' $SHOPERP/api/missions/my/progress)"
check "api-/missions/my/completions-401" "401" "$(curl -sk -o /dev/null -w '%{http_code}' $SHOPERP/api/missions/my/completions)"
check "api-/missions-admin-302" "302" "$(curl -sk -o /dev/null -w '%{http_code}' $SHOPERP/api/missions)"

# === 18-20. Customer profile API (Loyalty-C new) ===
echo "--- 6. Customer profile API (Loyalty-C) ---"
# Write JSON bodies to temp files to avoid shell quoting issues
echo '{"Birthday":"1990-01-01"}' > /tmp/bday.json
echo '{"ShareUrl":"https://facebook.com/test"}' > /tmp/share.json
check "api-/customer-profile/birthday-401" "401" "$(curl -sk -o /dev/null -w '%{http_code}' -X POST -H 'Content-Type: application/json' -d @/tmp/bday.json $SHOPERP/api/customer-profile/birthday)"
check "api-/customer-profile/pwa-installed-401" "401" "$(curl -sk -o /dev/null -w '%{http_code}' -X POST $SHOPERP/api/customer-profile/pwa-installed)"
check "api-/customer-profile/share-401" "401" "$(curl -sk -o /dev/null -w '%{http_code}' -X POST -H 'Content-Type: application/json' -d @/tmp/share.json $SHOPERP/api/customer-profile/share)"

# === 21. DB migration check (Missions table exists) ===
echo "--- 7. DB migration check ---"
MA_BODY=$(curl -sk $SHOPERP/api/missions/active)
if [ "$MA_BODY" = "[]" ] || echo "$MA_BODY" | grep -qE '\[.*\]'; then
  PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] db-missions-table-exists — /api/missions/active returned valid JSON array\n"
else
  FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] db-missions-table-exists — unexpected response: $MA_BODY\n"
fi

# === 22. Regression: redemption catalog API ===
echo "--- 8. Regression: redemption API ---"
check "api-/redemption/catalog/active-200" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $SHOPERP/api/redemption/catalog/active)"

# === 23-24. Error logs ===
echo "--- 9. Error logs ---"
GW_ERR=$(docker logs vanan-gateway --since 5m 2>&1 | grep -iE "error|exception" | grep -vi "OutboxMessages" | grep -v 'SELECT' | grep -vi "ECONNRESET" | grep -vi "Connection refused" | head -5)
if [ -z "$GW_ERR" ]; then
  PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] gateway-no-recent-errors\n"
else
  FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] gateway-errors: ${GW_ERR}\n"
fi
SE_ERR=$(docker logs vanan-shoperp --since 5m 2>&1 | grep -iE "error|exception" | grep -vi "OutboxMessages" | grep -v 'SELECT' | grep -vi "warn" | head -5)
if [ -z "$SE_ERR" ]; then
  PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] shoperp-no-recent-errors\n"
else
  FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] shoperp-errors: ${SE_ERR}\n"
fi

echo
echo "============================================"
echo "RV RESULTS: $PASS PASS / $FAIL FAIL"
echo "============================================"
echo
echo -e "$RESULTS"
[ $FAIL -gt 0 ] && exit 1 || exit 0
