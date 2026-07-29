#!/bin/bash
# Test redemption flow: get catalog + attempt redeem
# First need a valid token — use OTP flow for Bảo Ấn Lê (6D1CEB44)
# But 6D1CEB44 has no phone. Try with Fresh Test 3 (1A43AE1F, 100 points, phone 0900000055)

echo "=== 1. Send OTP ==="
curl -sk -D /tmp/r_headers -X POST -H 'Content-Type: application/json' \
  -d '{"phoneNumber":"0900000055"}' \
  https://api.khachvip.online/api/customers/otp/send
echo
OTP=$(grep -i 'X-Dev-OTP' /tmp/r_headers | tr -d '\r' | awk '{print $2}')
echo "OTP: $OTP"

echo "=== 2. Verify OTP ==="
VERIFY=$(curl -sk -X POST -H 'Content-Type: application/json' \
  -d "{\"phoneNumber\":\"0900000055\",\"otp\":\"$OTP\"}" \
  https://api.khachvip.online/api/customers/otp/verify)
echo "$VERIFY" | python3 -m json.tool 2>/dev/null
TOKEN=$(echo "$VERIFY" | python3 -c "import sys,json; print(json.load(sys.stdin)['customerToken'])" 2>/dev/null)
echo "Token: ${TOKEN:0:30}..."

echo "=== 3. GET /api/redemption/catalog/active ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/redemption/catalog/active | python3 -m json.tool 2>/dev/null

echo "=== 4. GET /api/loyalty/my (check balance) ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/loyalty/my | python3 -m json.tool 2>/dev/null
