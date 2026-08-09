#!/bin/bash
# RV Phase A V6+V7: Login as adminvanan1 + verify NavMenu + Dashboard
set +e
SHOPERP="https://app.khachvip.online"
PASS=0; FAIL=0
ok()   { echo "  [PASS] $1"; PASS=$((PASS+1)); }
fail() { echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }

echo "=== Login as adminvanan1 ==="
LOGIN_RESP=$(curl -sk -c /tmp/rv_cookies_owner.txt -X POST -H 'Content-Type: application/json' \
  -d '{"username":"adminvanan1","password":"Admin@123"}' \
  "$SHOPERP/api/platform/login")
echo "  Login response: $LOGIN_RESP"

if echo "$LOGIN_RESP" | grep -q '"success":true'; then
  ok "Login successful"
else
  # Try ShopERP's main login endpoint (not /api/platform/login)
  echo "  Trying /api/auth/login..."
  LOGIN_RESP2=$(curl -sk -c /tmp/rv_cookies_owner.txt -X POST -H 'Content-Type: application/json' \
    -d '{"username":"adminvanan1","password":"Admin@123"}' \
    "$SHOPERP/api/auth/login")
  echo "  /api/auth/login response: $LOGIN_RESP2"
  if echo "$LOGIN_RESP2" | grep -q '"success":true\|token'; then
    ok "Login successful via /api/auth/login"
  else
    fail "Login failed — cannot verify V6/V7"
    echo "========================================"
    echo "  RV V6+V7 (with login): $PASS PASS, $FAIL FAIL"
    echo "========================================"
    exit 0
  fi
fi
echo ""

echo "[V7] NavMenu contains 'loyalty/dashboard' link (after login)"
NAV_HTML=$(curl -sk -b /tmp/rv_cookies_owner.txt "$SHOPERP/")
if echo "$NAV_HTML" | grep -q "loyalty/dashboard"; then
  ok "NavMenu has /loyalty/dashboard link"
else
  fail "NavMenu missing /loyalty/dashboard link"
  echo "  HTML length: ${#NAV_HTML}"
  # Check if still on login page
  if echo "$NAV_HTML" | grep -qi "login"; then
    echo "  → Still on login page (cookie not set)"
  fi
fi
echo ""

echo "[V6] Dashboard page loads + contains 4 stat card labels"
DASH_HTML=$(curl -sk -b /tmp/rv_cookies_owner.txt "$SHOPERP/loyalty/dashboard")
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
    fail "Dashboard HTML missing '$label' (may need JS render)"
  fi
done
echo ""

echo "[V8] GET /api/loyalty/dashboard with auth cookie"
API_RESP=$(curl -sk -b /tmp/rv_cookies_owner.txt -w "\n%{http_code}" "$SHOPERP/api/loyalty/dashboard")
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
