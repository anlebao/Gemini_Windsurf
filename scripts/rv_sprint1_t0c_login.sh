#!/bin/bash
# CC-S1-T0c (Sprint 1) Runtime Verification on VPS
# Tests: customer login simplify — no OTP form, 3 buttons (Google + Facebook + Guest)
set +e
PASS=0; FAIL=0; RESULTS=""

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
    PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] $name — matched '$expected'\n"
  else
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] $name — expected '$expected', got '$actual'\n"
  fi
}
check_not_contains() {
  local name="$1"; local needle="$2"; local haystack="$3"
  if echo "$haystack" | grep -q "$needle"; then
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] $name — should NOT contain '$needle'\n"
  else
    PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] $name — does NOT contain '$needle' (correct)\n"
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
GATEWAY="https://api.khachvip.online"

echo "=== RV-S1-T0c-1: Login page loads ==="
LOGIN_HTML=$(curl -sk $KHACHLINK/login)
check "rv-s1-login-page-200" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $KHACHLINK/login)"

echo
echo "=== RV-S1-T0c-2: Login page has Google button ==="
check_contains "rv-s1-google-button" "Đăng nhập với Google" "$LOGIN_HTML"

echo
echo "=== RV-S1-T0c-3: Login page has Facebook button ==="
check_contains "rv-s1-facebook-button" "Đăng nhập với Facebook" "$LOGIN_HTML"

echo
echo "=== RV-S1-T0c-4: Login page has Guest button ==="
check_contains "rv-s1-guest-button" "Tiếp tục as Guest" "$LOGIN_HTML"

echo
echo "=== RV-S1-T0c-5: Login page NO OTP form (SMS removed from primary flow) ==="
check_not_contains "rv-s1-no-otp-send" "Gửi mã OTP" "$LOGIN_HTML"
check_not_contains "rv-s1-no-otp-verify" "Xác nhận" "$LOGIN_HTML"

echo
echo "=== RV-S1-T0c-6: OTP endpoints STILL WORK (kept for Sprint 6 collaborator toggle) ==="
OTP_SEND_CODE=$(curl -sk -o /dev/null -w '%{http_code}' -X POST $GATEWAY/api/customer-identity/otp/send \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber":"0901234567"}')
check "rv-s1-otp-send-endpoint-200" "200" "$OTP_SEND_CODE"

echo
echo "=== RV-S1-T0c-7: Facebook login endpoint exists (stub redirect) ==="
FB_LOGIN_CODE=$(curl -sk -o /dev/null -w '%{http_code}' $GATEWAY/api/auth/facebook/login)
check_in "rv-s1-facebook-endpoint-302" "302|301" "$FB_LOGIN_CODE"

echo
echo "=== RV-S1-T0c-8: Google login endpoint still works ==="
GOOGLE_LOGIN_CODE=$(curl -sk -o /dev/null -w '%{http_code}' $GATEWAY/api/auth/google/login)
check_in "rv-s1-google-endpoint-302" "302|301" "$GOOGLE_LOGIN_CODE"

echo
echo "=== RV-S1-T0c-9: Device fingerprint endpoint still works (CC-S0-T3 regression) ==="
DR_CODE=$(curl -sk -o /dev/null -w '%{http_code}' -X POST $GATEWAY/api/customer-identity/device/register \
  -H "Content-Type: application/json" \
  -d '{"deviceToken":"test","fingerprintHash":"abc"}')
check "rv-s1-device-register-401" "401" "$DR_CODE"

echo
echo "=== RV-S1-T0c-10: Fingerprint JS still loads (CC-S0-T3 regression) ==="
check "rv-s1-fingerprint-js-200" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $KHACHLINK/js/fingerprint.js)"

echo
echo "========================================"
echo "CC-S1-T0c RV SUMMARY: PASS=$PASS  FAIL=$FAIL"
echo "========================================"
echo
printf "$RESULTS"
echo
if [ "$FAIL" -eq 0 ]; then
  echo "ALL CC-S1-T0c CHECKS PASSED"
  exit 0
else
  echo "FAILURES DETECTED — review above"
  exit 1
fi
