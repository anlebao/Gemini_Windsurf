#!/bin/bash
# RV Phase A V6+V7: Login via /Login form (cookie auth) + verify dashboard
set +e
SHOPERP="https://app.khachvip.online"
PASS=0; FAIL=0
ok()   { echo "  [PASS] $1"; PASS=$((PASS+1)); }
fail() { echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }

echo "=== Step 1: GET /Login to fetch anti-forgery token ==="
LOGIN_HTML=$(curl -sk -c /tmp/rv_login_cookies.txt "$SHOPERP/Login")
echo "  Login page length: ${#LOGIN_HTML}"

# Extract anti-forgery token (ASP.NET Core: __RequestVerificationToken)
TOKEN=$(echo "$LOGIN_HTML" | grep -oP 'name="__RequestVerificationToken" type="hidden" value="\K[^"]+' | head -1)
if [ -z "$TOKEN" ]; then
  # Try alternate format
  TOKEN=$(echo "$LOGIN_HTML" | grep -oP 'value="\K[^"]+(?=" name="__RequestVerificationToken)' | head -1)
fi
if [ -z "$TOKEN" ]; then
  TOKEN=$(echo "$LOGIN_HTML" | grep -i 'RequestVerificationToken' | grep -oP 'value="\K[^"]+' | head -1)
fi
echo "  Anti-forgery token: ${TOKEN:0:50}..."

if [ -z "$TOKEN" ]; then
  fail "Could not extract anti-forgery token"
  echo "  Login HTML snippet:"
  echo "$LOGIN_HTML" | grep -i 'token\|csrf\|antiforgery' | head -5
  echo "========================================"
  echo "  RV V6+V7: $PASS PASS, $FAIL FAIL"
  echo "========================================"
  exit 0
fi
ok "Anti-forgery token extracted"
echo ""

echo "=== Step 2: POST /Login with credentials ==="
LOGIN_RESP=$(curl -sk -b /tmp/rv_login_cookies.txt -c /tmp/rv_login_cookies.txt \
  -X POST "$SHOPERP/Login" \
  -d "username=adminvanan1&password=Admin%40123&rememberMe=true&__RequestVerificationToken=$TOKEN" \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -w "\n%{http_code}" -o /tmp/rv_login_resp.txt)
CODE=$(echo "$LOGIN_RESP" | tail -1)
echo "  POST /Login response code: $CODE"
echo "  Response headers saved"

# Check if we got a redirect (302 = success, 200 = form re-rendered with errors)
if [ "$CODE" = "302" ] || [ "$CODE" = "303" ]; then
  ok "Login successful (302 redirect)"
elif [ "$CODE" = "200" ]; then
  # Check if response contains error
  if grep -qi "error\|invalid\|sai" /tmp/rv_login_resp.txt; then
    fail "Login failed (200 with error)"
    grep -i "error\|invalid\|sai" /tmp/rv_login_resp.txt | head -3
  else
    ok "Login returned 200 (may have succeeded)"
  fi
else
  fail "Login returned $CODE"
fi
echo ""

echo "[V7] NavMenu contains 'loyalty/dashboard' link (after login)"
NAV_HTML=$(curl -sk -b /tmp/rv_login_cookies.txt "$SHOPERP/")
if echo "$NAV_HTML" | grep -q "loyalty/dashboard"; then
  ok "NavMenu has /loyalty/dashboard link"
else
  fail "NavMenu missing /loyalty/dashboard link"
  echo "  HTML length: ${#NAV_HTML}"
  if echo "$NAV_HTML" | grep -qi "login"; then
    echo "  → Still on login page (cookie not set)"
  fi
fi
echo ""

echo "[V6] Dashboard page loads + contains 4 stat card labels"
DASH_HTML=$(curl -sk -b /tmp/rv_login_cookies.txt "$SHOPERP/loyalty/dashboard")
DASH_LEN=${#DASH_HTML}
echo "  Dashboard HTML length: $DASH_LEN"
if [ "$DASH_LEN" -gt 100 ]; then
  ok "Dashboard page returned content (length > 100)"
else
  fail "Dashboard page empty or redirect (length=$DASH_LEN)"
fi
for label in "Điểm chờ đổi" "Đã đổi" "Điểm CTKM chờ thưởng" "Dự trù điểm thưởng"; do
  if echo "$DASH_HTML" | grep -q "$label"; then
    ok "Dashboard HTML contains '$label'"
  else
    fail "Dashboard HTML missing '$label' (may need JS render — Blazor Server)"
  fi
done
echo ""

echo "[V8] GET /api/loyalty/dashboard with auth cookie"
API_RESP=$(curl -sk -b /tmp/rv_login_cookies.txt -w "\n%{http_code}" "$SHOPERP/api/loyalty/dashboard")
API_CODE=$(echo "$API_RESP" | tail -1)
API_BODY=$(echo "$API_RESP" | head -n -1)
if [ "$API_CODE" = "200" ]; then
  ok "GET /api/loyalty/dashboard: 200 (with auth)"
  echo "  Response: $API_BODY"
elif [ "$API_CODE" = "401" ]; then
  fail "GET /api/loyalty/dashboard: 401 (auth cookie not accepted)"
else
  fail "GET /api/loyalty/dashboard: $API_CODE"
  echo "  Body: $API_BODY"
fi
echo ""

echo "========================================"
echo "  RV V6+V7+V8 (with login): $PASS PASS, $FAIL FAIL"
echo "========================================"
