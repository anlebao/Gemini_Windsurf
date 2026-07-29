#!/bin/bash
# CC-S0-T3 (Sprint 0.5) Runtime Verification on VPS
# Tests: device fingerprint wire-up end-to-end
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
    PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] $name — matched '$expected' (got '$actual')\n"
  else
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] $name — expected '$expected', got '$actual'\n"
  fi
}
check_not() {
  local name="$1"; local expected="$2"; local actual="$3"
  if [ "$actual" != "$expected" ]; then
    PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] $name — not '$expected' (got '$actual')\n"
  else
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] $name — should NOT be '$expected'\n"
  fi
}

SHOPERP="https://khachvip.online"
KHACHLINK="https://diemthuong.khachvip.online"
GATEWAY="https://api.khachvip.online"

echo "=== RV-S0.5-1: Gateway health ==="
check "rv-s0.5-1-gateway-health" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $GATEWAY/api/health)"

echo
echo "=== RV-S0.5-2: Device register endpoint exists (401 without token, NOT 404) ==="
REGISTER_CODE=$(curl -sk -o /dev/null -w '%{http_code}' -X POST $GATEWAY/api/customer-identity/device/register \
  -H "Content-Type: application/json" \
  -d '{"deviceToken":"test","fingerprintHash":"abc"}')
check_not "rv-s0.5-2-endpoint-not-404" "404" "$REGISTER_CODE"
check "rv-s0.5-2-endpoint-401-without-token" "401" "$REGISTER_CODE"

echo
echo "=== RV-S0.5-3: Device register endpoint 400 with token but empty fingerprint ==="
# Need a valid token — try OTP flow first
OTP_RESP=$(curl -sk -X POST $GATEWAY/api/customer-identity/otp/send \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber":"0901234567"}')
DEV_OTP=$(curl -sk -D - -X POST $GATEWAY/api/customer-identity/otp/send \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber":"0901234567"}' 2>/dev/null | grep -i 'X-Dev-OTP' | awk '{print $2}' | tr -d '\r\n')
echo "Dev OTP: $DEV_OTP"

if [ -n "$DEV_OTP" ]; then
  VERIFY_RESP=$(curl -sk -X POST $GATEWAY/api/customer-identity/otp/verify \
    -H "Content-Type: application/json" \
    -d "{\"phoneNumber\":\"0901234567\",\"otp\":\"$DEV_OTP\",\"displayName\":\"RV Test\"}")
  TOKEN=$(echo "$VERIFY_RESP" | grep -o '"customerToken":"[^"]*"' | cut -d'"' -f4)
  echo "Token obtained: ${TOKEN:0:20}..."

  if [ -n "$TOKEN" ]; then
    # Test 400 with empty fingerprint
    BAD_REQ_CODE=$(curl -sk -o /dev/null -w '%{http_code}' -X POST $GATEWAY/api/customer-identity/device/register \
      -H "Content-Type: application/json" \
      -H "X-Customer-Token: $TOKEN" \
      -d '{"deviceToken":"test","fingerprintHash":""}')
    check "rv-s0.5-3-empty-fingerprint-400" "400" "$BAD_REQ_CODE"

    # Test 200 with valid fingerprint (real registration)
    GOOD_RESP=$(curl -sk -X POST $GATEWAY/api/customer-identity/device/register \
      -H "Content-Type: application/json" \
      -H "X-Customer-Token: $TOKEN" \
      -d '{"deviceToken":"rvtest-token-001","fingerprintHash":"a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2","fingerprintSignals":"{}","userAgent":"RV-Test","platform":"Linux"}')
    echo "Register response: $GOOD_RESP"
    check_in "rv-s0.5-4-register-success" "deviceId" "$GOOD_RESP"
    check_in "rv-s0.5-4-register-active" "\"isActive\":true" "$GOOD_RESP"
  else
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] rv-s0.5-3 — could not obtain token from OTP flow\n"
  fi
else
  echo "WARN: Dev OTP not exposed (production mode) — skipping authenticated tests"
  FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] rv-s0.5-3 — Dev OTP not exposed, cannot test authenticated flow\n"
fi

echo
echo "=== RV-S0.5-5: DeviceRegistrations table exists on PG ==="
DR_EXISTS=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT count(*) FROM information_schema.tables WHERE table_name='DeviceRegistrations';")
check "rv-s0.5-5-device-registrations-table" "1" "$(echo $DR_EXISTS | tr -d '[:space:]')"

echo
echo "=== RV-S0.5-6: DeviceRegistration row created (if register succeeded) ==="
if [ -n "$TOKEN" ]; then
  DR_COUNT=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT count(*) FROM \"DeviceRegistrations\" WHERE \"DeviceToken\"='rvtest-token-001';")
  check "rv-s0.5-6-device-row-created" "1" "$(echo $DR_COUNT | tr -d '[:space:]')"
fi

echo
echo "=== RV-S0.5-7: Fingerprint JS loads on KhachLink ==="
FP_JS_CODE=$(curl -sk -o /dev/null -w '%{http_code}' $KHACHLINK/js/fingerprint.js)
check "rv-s0.5-7-fingerprint-js-200" "200" "$FP_JS_CODE"
FP_JS_CONTENT=$(curl -sk $KHACHLINK/js/fingerprint.js)
check_in "rv-s0.5-7-fingerprint-js-has-collect" "window.fingerprint" "$FP_JS_CONTENT"

echo
echo "=== RV-S0.5-8: FingerprintJS library loads ==="
FP_LIB_CODE=$(curl -sk -o /dev/null -w '%{http_code}' $KHACHLINK/lib/fingerprintjs/fingerprint.js)
check "rv-s0.5-8-fingerprintjs-lib-200" "200" "$FP_LIB_CODE"

echo
echo "========================================"
echo "CC-S0-T3 RV SUMMARY: PASS=$PASS  FAIL=$FAIL"
echo "========================================"
echo
printf "$RESULTS"
echo
if [ "$FAIL" -eq 0 ]; then
  echo "ALL CC-S0-T3 CHECKS PASSED"
  exit 0
else
  echo "FAILURES DETECTED — review above"
  exit 1
fi
