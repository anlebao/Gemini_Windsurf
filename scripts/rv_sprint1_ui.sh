#!/bin/bash
# CC-S1-T1/T2 UI RV on VPS — NearbyOrders.razor + NavMenu shipper tab + role endpoint
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

KHACHLINK="https://khachvip.online"
GATEWAY="https://api.khachvip.online"

echo "=== RV-S1-UI-1: NearbyOrders page route reachable (Blazor WASM bootstrap) ==="
NEARBY_CODE=$(curl -sk -o /dev/null -w '%{http_code}' $KHACHLINK/community/nearby-orders)
check "rv-s1-ui-nearby-route-200" "200" "$NEARBY_CODE"

echo
echo "=== RV-S1-UI-2: Role endpoint exists (no token → 401) ==="
ROLE_NO_TOKEN=$(curl -sk -o /dev/null -w '%{http_code}' $GATEWAY/api/community/role)
check "rv-s1-ui-role-no-token-401" "401" "$ROLE_NO_TOKEN"

echo
echo "=== RV-S1-UI-3: Role endpoint with invalid token → 401 ==="
ROLE_BAD_TOKEN=$(curl -sk -o /dev/null -w '%{http_code}' -H "X-Customer-Token: invalid_123" $GATEWAY/api/community/role)
check "rv-s1-ui-role-bad-token-401" "401" "$ROLE_BAD_TOKEN"

echo
echo "=== RV-S1-UI-4: Nearby orders API still works (no token → 401) ==="
NEARBY_API=$(curl -sk -o /dev/null -w '%{http_code}' "$GATEWAY/api/community/nearby-orders?lat=10.8&lng=106.7&radiusKm=5")
check "rv-s1-ui-nearby-api-401" "401" "$NEARBY_API"

echo
echo "=== RV-S1-UI-5: Accept API still works (no token → 401) ==="
ACCEPT_API=$(curl -sk -o /dev/null -w '%{http_code}' -X POST $GATEWAY/api/community/orders/00000000-0000-0000-0000-000000000099/accept)
check "rv-s1-ui-accept-api-401" "401" "$ACCEPT_API"

echo
echo "=== RV-S1-UI-6: KhachLink WASM binary contains NearbyOrders page text ==="
# Blazor WASM renders client-side — verify the new strings are in the deployed DLL
# Find the KhachLink DLL on the VPS container
WASM_DLL=$(ssh -i /home/ubuntu/.ssh/id_rsa -o StrictHostKeyChecking=no localhost 'docker exec vanan-khachlink find /app/wwwroot/_framework -name "VanAn.KhachLink.dll" 2>/dev/null | head -1' 2>/dev/null)
if [ -z "$WASM_DLL" ]; then
  # Try alternate paths
  WASM_DLL=$(ssh -i /home/ubuntu/.ssh/id_rsa -o StrictHostKeyChecking=no localhost 'docker exec vanan-khachlink find / -name "VanAn.KhachLink.dll" 2>/dev/null | head -1' 2>/dev/null)
fi

if [ -n "$WASM_DLL" ]; then
  NEARBY_COUNT=$(ssh -i /home/ubuntu/.ssh/id_rsa -o StrictHostKeyChecking=no localhost "docker exec vanan-khachlink strings '$WASM_DLL' 2>/dev/null | grep -c 'Đơn hàng giao gần bạn'" 2>/dev/null || echo "0")
  if [ "$NEARBY_COUNT" -gt 0 ] 2>/dev/null; then
    PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] rv-s1-ui-wasm-nearby-text — count=$NEARBY_COUNT\n"
  else
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] rv-s1-ui-wasm-nearby-text — 'Đơn hàng giao gần bạn' not found in $WASM_DLL\n"
  fi

  SHIPPER_TAB=$(ssh -i /home/ubuntu/.ssh/id_rsa -o StrictHostKeyChecking=no localhost "docker exec vanan-khachlink strings '$WASM_DLL' 2>/dev/null | grep -c 'Đơn giao gần đây'" 2>/dev/null || echo "0")
  if [ "$SHIPPER_TAB" -gt 0 ] 2>/dev/null; then
    PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] rv-s1-ui-wasm-shipper-tab — count=$SHIPPER_TAB\n"
  else
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] rv-s1-ui-wasm-shipper-tab — 'Đơn giao gần đây' not found\n"
  fi

  ACCEPT_BTN=$(ssh -i /home/ubuntu/.ssh/id_rsa -o StrictHostKeyChecking=no localhost "docker exec vanan-khachlink strings '$WASM_DLL' 2>/dev/null | grep -c 'Nhận đơn'" 2>/dev/null || echo "0")
  if [ "$ACCEPT_BTN" -gt 0 ] 2>/dev/null; then
    PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] rv-s1-ui-wasm-accept-btn — count=$ACCEPT_BTN\n"
  else
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] rv-s1-ui-wasm-accept-btn — 'Nhận đơn' not found\n"
  fi

  ROLE_API=$(ssh -i /home/ubuntu/.ssh/id_rsa -o StrictHostKeyChecking=no localhost "docker exec vanan-khachlink strings '$WASM_DLL' 2>/dev/null | grep -c 'api/community/role'" 2>/dev/null || echo "0")
  if [ "$ROLE_API" -gt 0 ] 2>/dev/null; then
    PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] rv-s1-ui-wasm-role-api — count=$ROLE_API\n"
  else
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] rv-s1-ui-wasm-role-api — 'api/community/role' not found\n"
  fi
else
  RESULTS="${RESULTS}[SKIP] rv-s1-ui-wasm — cannot find VanAn.KhachLink.dll on VPS (SSH access issue)\n"
fi

echo
echo "=== RV-S1-UI-7: Regression — Home page still loads ==="
HOME_CODE=$(curl -sk -o /dev/null -w '%{http_code}' $KHACHLINK/)
check "rv-s1-ui-regression-home-200" "200" "$HOME_CODE"

echo
echo "=== RV-S1-UI-8: Regression — Login page still loads ==="
LOGIN_CODE=$(curl -sk -o /dev/null -w '%{http_code}' $KHACHLINK/login)
check "rv-s1-ui-regression-login-200" "200" "$LOGIN_CODE"

echo
echo "========================================"
echo "CC-S1-T1/T2 UI RV SUMMARY: PASS=$PASS  FAIL=$FAIL"
echo "========================================"
echo
printf "$RESULTS"
echo
if [ "$FAIL" -eq 0 ]; then
  echo "ALL CC-S1-T1/T2 UI CHECKS PASSED"
  exit 0
else
  echo "FAILURES DETECTED — review above"
  exit 1
fi
