#!/bin/bash
# Verify SC15 with correct endpoint path: /api/auth/google/login
set +e
PASS=0; FAIL=0; RESULTS=""

check_in() {
  local name="$1"; local expected="$2"; local actual="$3"
  if echo "$actual" | grep -qE "$expected"; then
    PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] $name — matched '$expected' (got '$actual')\n"
  else
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] $name — expected '$expected', got '$actual'\n"
  fi
}
check_contains() {
  local name="$1"; local needle="$2"; local haystack="$3"
  if echo "$haystack" | grep -q "$needle"; then
    PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] $name — contains '$needle'\n"
  else
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] $name — missing '$needle'\n"
  fi
}

SHOPERP="https://khachvip.online"
KHACHLINK="https://diemthuong.khachvip.online"

echo "=== SC15 (corrected): Google Social Login regression ==="

# Correct path: /api/auth/google/login — should redirect (302) to Google OAuth
# OR return 200 with redirect URL in body. 404 = endpoint missing (regression).
LOGIN_CODE=$(curl -sk -o /dev/null -w '%{http_code}' $SHOPERP/api/auth/google/login)
echo "  GET /api/auth/google/login → $LOGIN_CODE"
check_in "sc15-google-login-endpoint-exists" "200|302|401|400" "$LOGIN_CODE"

# Inspect response body / headers for Google OAuth URL
LOGIN_BODY=$(curl -sk $SHOPERP/api/auth/google/login)
LOGIN_HEADERS=$(curl -sk -I $SHOPERP/api/auth/google/login)
echo "  Body (first 200 chars): $(echo $LOGIN_BODY | head -c 200)"
echo "  Headers:"
echo "$LOGIN_HEADERS" | head -10

# Callback endpoint should also exist (not 404)
CB_CODE=$(curl -sk -o /dev/null -w '%{http_code}' $SHOPERP/api/auth/google/callback)
echo "  GET /api/auth/google/callback → $CB_CODE"
check_in "sc15-google-callback-endpoint-exists" "200|302|400|401" "$CB_CODE"

# KhachLink appsettings — check via wwwroot (WASM static config)
KL_APPSETTINGS=$(curl -sk $KHACHLINK/appsettings.json)
check_contains "sc15-khachlink-appsettings-has-google" "Google" "$KL_APPSETTINGS"
check_contains "sc15-khachlink-appsettings-has-client-id" "ClientId" "$KL_APPSETTINGS"

# KhachLink Google login URL configured in CD .env (deployed to ShopERP)
SE_APPSETTINGS=$(docker exec vanan-shoperp cat /app/appsettings.json 2>/dev/null)
check_contains "sc15-shoperp-appsettings-has-google" "Google" "$SE_APPSETTINGS"

echo
echo "========================================"
echo "SC15 RV SUMMARY: PASS=$PASS  FAIL=$FAIL"
echo "========================================"
echo
printf "$RESULTS"
echo
[ "$FAIL" -eq 0 ] && echo "ALL PASS" || echo "FAILURES DETECTED"
exit $FAIL
