#!/bin/bash
# RV V6+V7: Login to ShopERP as Owner, verify NavMenu + Dashboard
set +e
SHOPERP="https://app.khachvip.online"
PASS=0; FAIL=0
ok()   { echo "  [PASS] $1"; PASS=$((PASS+1)); }
fail() { echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }

echo "[V7-LOGIN] Login as Owner + check NavMenu link"
# Try login endpoint — adjust if different
LOGIN_RESP=$(curl -sk -c /tmp/rv_cookies.txt -X POST -H 'Content-Type: application/json' \
  -d '{"email":"owner@vanan.local","password":"Owner@2024!"}' \
  "$SHOPERP/api/auth/login")
echo "  Login response: $LOGIN_RESP"

# Try GET / with cookies
NAV_HTML=$(curl -sk -b /tmp/rv_cookies.txt "$SHOPERP/")
if echo "$NAV_HTML" | grep -q "loyalty/dashboard"; then
  ok "NavMenu has /loyalty/dashboard link (after login)"
else
  fail "NavMenu missing /loyalty/dashboard link (after login)"
  echo "  HTML length: ${#NAV_HTML}"
  # Check if we got redirected to login
  if echo "$NAV_HTML" | grep -qi "login"; then
    echo "  → Redirected to login (auth failed)"
  fi
fi
echo ""

echo "[V6-LOGIN] Dashboard page with auth cookie"
DASH_HTML=$(curl -sk -b /tmp/rv_cookies.txt "$SHOPERP/loyalty/dashboard")
if echo "$DASH_HTML" | grep -q "Điểm chờ đổi"; then
  ok "Dashboard HTML contains 'Điểm chờ đổi'"
else
  fail "Dashboard HTML missing 'Điểm chờ đổi'"
  echo "  HTML length: ${#DASH_HTML}"
fi
echo ""

echo "========================================"
echo "  RV V6+V7 (with login): $PASS PASS, $FAIL FAIL"
echo "========================================"
