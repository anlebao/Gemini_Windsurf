#!/bin/bash
set +e
KHACHLINK="https://diemthuong.khachvip.online"
GATEWAY="https://api.khachvip.online"
PASS=0; FAIL=0

# 1. NearbyOrders page route
NEARBY_CODE=$(curl -sk -o /dev/null -w "%{http_code}" $KHACHLINK/community/nearby-orders)
if [ "$NEARBY_CODE" = "200" ]; then echo "[PASS] rv-s1-ui-nearby-route-200 — $NEARBY_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s1-ui-nearby-route-200 — got $NEARBY_CODE"; FAIL=$((FAIL+1)); fi

# 2. Role endpoint no token
ROLE_CODE=$(curl -sk -o /dev/null -w "%{http_code}" $GATEWAY/api/community/role)
if [ "$ROLE_CODE" = "401" ]; then echo "[PASS] rv-s1-ui-role-no-token-401 — $ROLE_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s1-ui-role-no-token-401 — got $ROLE_CODE"; FAIL=$((FAIL+1)); fi

# 3. Role endpoint invalid token
ROLE_BAD=$(curl -sk -o /dev/null -w "%{http_code}" -H "X-Customer-Token: invalid_123" $GATEWAY/api/community/role)
if [ "$ROLE_BAD" = "401" ]; then echo "[PASS] rv-s1-ui-role-bad-token-401 — $ROLE_BAD"; PASS=$((PASS+1)); else echo "[FAIL] rv-s1-ui-role-bad-token-401 — got $ROLE_BAD"; FAIL=$((FAIL+1)); fi

# 4. Nearby API no token
NEARBY_API=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/api/community/nearby-orders?lat=10.8&lng=106.7&radiusKm=5")
if [ "$NEARBY_API" = "401" ]; then echo "[PASS] rv-s1-ui-nearby-api-401 — $NEARBY_API"; PASS=$((PASS+1)); else echo "[FAIL] rv-s1-ui-nearby-api-401 — got $NEARBY_API"; FAIL=$((FAIL+1)); fi

# 5. Accept API no token
ACCEPT_API=$(curl -sk -o /dev/null -w "%{http_code}" -X POST $GATEWAY/api/community/orders/00000000-0000-0000-0000-000000000099/accept)
if [ "$ACCEPT_API" = "401" ]; then echo "[PASS] rv-s1-ui-accept-api-401 — $ACCEPT_API"; PASS=$((PASS+1)); else echo "[FAIL] rv-s1-ui-accept-api-401 — got $ACCEPT_API"; FAIL=$((FAIL+1)); fi

# 6. WASM binary — CommunityHttpService
CHS_COUNT=$(docker exec vanan-khachlink grep -ac "CommunityHttpService" /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null)
if [ "$CHS_COUNT" -gt 0 ] 2>/dev/null; then echo "[PASS] rv-s1-ui-wasm-communityhttpservice — count=$CHS_COUNT"; PASS=$((PASS+1)); else echo "[FAIL] rv-s1-ui-wasm-communityhttpservice — count=$CHS_COUNT"; FAIL=$((FAIL+1)); fi

# 7. WASM binary — NearbyOrders page
NO_COUNT=$(docker exec vanan-khachlink grep -ac "NearbyOrders" /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null)
if [ "$NO_COUNT" -gt 0 ] 2>/dev/null; then echo "[PASS] rv-s1-ui-wasm-nearbyorders — count=$NO_COUNT"; PASS=$((PASS+1)); else echo "[FAIL] rv-s1-ui-wasm-nearbyorders — count=$NO_COUNT"; FAIL=$((FAIL+1)); fi

# 8. WASM binary — GetIsShipper (role check method)
GIS_COUNT=$(docker exec vanan-khachlink grep -ac "GetIsShipper" /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null)
if [ "$GIS_COUNT" -gt 0 ] 2>/dev/null; then echo "[PASS] rv-s1-ui-wasm-getisshipper — count=$GIS_COUNT"; PASS=$((PASS+1)); else echo "[FAIL] rv-s1-ui-wasm-getisshipper — count=$GIS_COUNT"; FAIL=$((FAIL+1)); fi

# 9. WASM binary — AcceptOrder method
AO_COUNT=$(docker exec vanan-khachlink grep -ac "AcceptOrder" /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null)
if [ "$AO_COUNT" -gt 0 ] 2>/dev/null; then echo "[PASS] rv-s1-ui-wasm-acceptorder — count=$AO_COUNT"; PASS=$((PASS+1)); else echo "[FAIL] rv-s1-ui-wasm-acceptorder — count=$AO_COUNT"; FAIL=$((FAIL+1)); fi

# 10. Regression — Home page
HOME_CODE=$(curl -sk -o /dev/null -w "%{http_code}" $KHACHLINK/)
if [ "$HOME_CODE" = "200" ]; then echo "[PASS] rv-s1-ui-regression-home-200 — $HOME_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s1-ui-regression-home-200 — got $HOME_CODE"; FAIL=$((FAIL+1)); fi

# 11. Regression — Login page
LOGIN_CODE=$(curl -sk -o /dev/null -w "%{http_code}" $KHACHLINK/login)
if [ "$LOGIN_CODE" = "200" ]; then echo "[PASS] rv-s1-ui-regression-login-200 — $LOGIN_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s1-ui-regression-login-200 — got $LOGIN_CODE"; FAIL=$((FAIL+1)); fi

# 12. Regression — OTP endpoint
OTP_CODE=$(curl -sk -o /dev/null -w "%{http_code}" -X POST $GATEWAY/api/customer-identity/otp/send -H "Content-Type: application/json" -d '{"phoneNumber":"0901234567"}')
if [ "$OTP_CODE" = "200" ]; then echo "[PASS] rv-s1-ui-regression-otp-200 — $OTP_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s1-ui-regression-otp-200 — got $OTP_CODE"; FAIL=$((FAIL+1)); fi

echo ""
echo "========================================"
echo "CC-S1-T1/T2 UI RV SUMMARY: PASS=$PASS  FAIL=$FAIL"
echo "========================================"
