#!/bin/bash
# Verify SC4, SC5, SC15 on VPS
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
GW_IP=$(docker inspect vanan-gateway --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
GATEWAY="http://$GW_IP"

echo "=== SC4: SalesReferral 3 fields (RiskScore, RiskFactors, HoldUntil) ==="
SR_COLS=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT count(*) FROM information_schema.columns WHERE table_name='SalesReferrals' AND column_name IN ('RiskScore','RiskFactors','HoldUntil');")
check "sc4-salesreferral-3-fields" "3" "$(echo $SR_COLS | tr -d '[:space:]')"
for c in RiskScore RiskFactors HoldUntil; do
  EXISTS=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT count(*) FROM information_schema.columns WHERE table_name='SalesReferrals' AND column_name='$c';")
  check "sc4-salesreferral-col-$c" "1" "$(echo $EXISTS | tr -d '[:space:]')"
done

echo
echo "=== SC5: AppInstallAttribution 4 fields (RiskScore, RiskFactors, HoldUntil, DeviceRegistrationId) ==="
AIA_COLS=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT count(*) FROM information_schema.columns WHERE table_name='AppInstallAttributions' AND column_name IN ('RiskScore','RiskFactors','HoldUntil','DeviceRegistrationId');")
check "sc5-appinstall-4-fields" "4" "$(echo $AIA_COLS | tr -d '[:space:]')"
for c in RiskScore RiskFactors HoldUntil DeviceRegistrationId; do
  EXISTS=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT count(*) FROM information_schema.columns WHERE table_name='AppInstallAttributions' AND column_name='$c';")
  check "sc5-appinstall-col-$c" "1" "$(echo $EXISTS | tr -d '[:space:]')"
done

echo
echo "=== SC15: Google Social Login regression (existing SocialAuthController) ==="
# Google login start endpoint — should return 200 or 302 redirect to Google OAuth
# Without auth, the endpoint should NOT 500 (regression = no crash)
GOOGLE_LOGIN_CODE=$(curl -sk -o /dev/null -w '%{http_code}' $SHOPERP/api/auth/google-login)
check_in "sc15-google-login-no-500" "200|302|401|400" "$GOOGLE_LOGIN_CODE"
# Also check the KhachLink Google login URL is configured
KL_APPSETTINGS=$(docker exec vanan-khachlink cat /app/wwwroot/appsettings.json 2>/dev/null)
check_contains "sc15-khachlink-google-config" "Google" "$KL_APPSETTINGS"

# Verify Google KhachLink login URL is reachable (configured in CD .env)
check "sc15-khachlink-login-page-200" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $KHACHLINK/login)"

echo
echo "========================================"
echo "GAP RV SUMMARY: PASS=$PASS  FAIL=$FAIL"
echo "========================================"
echo
printf "$RESULTS"
echo
if [ "$FAIL" -eq 0 ]; then
  echo "ALL GAP CHECKS PASSED"
  exit 0
else
  echo "FAILURES DETECTED — review above"
  exit 1
fi
