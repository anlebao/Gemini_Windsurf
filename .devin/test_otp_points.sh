#!/bin/bash
# End-to-end test: OTP verify → should award 100 points from OtpVerify mission
PHONE="0900000099"
echo "=== 1. Send OTP to $PHONE ==="
curl -sk -D /tmp/headers_otp -X POST -H 'Content-Type: application/json' \
  -d "{\"phoneNumber\":\"$PHONE\"}" \
  https://api.khachvip.online/api/customers/otp/send
echo
OTP=$(grep -i 'X-Dev-OTP' /tmp/headers_otp | tr -d '\r' | awk '{print $2}')
echo "OTP: $OTP"

echo "=== 2. Verify OTP ==="
VERIFY_RESP=$(curl -sk -X POST -H 'Content-Type: application/json' \
  -d "{\"phoneNumber\":\"$PHONE\",\"otp\":\"$OTP\",\"DisplayName\":\"Mission Test Customer\"}" \
  https://api.khachvip.online/api/customers/otp/verify)
echo "$VERIFY_RESP" | python3 -m json.tool 2>/dev/null || echo "$VERIFY_RESP"

TOKEN=$(echo "$VERIFY_RESP" | python3 -c "import sys,json; print(json.load(sys.stdin)['customerToken'])" 2>/dev/null)
echo "Token: ${TOKEN:0:30}..."

echo "=== 3. GET /api/customers/me (expect pointBalance: 100) ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/customers/me | python3 -m json.tool 2>/dev/null

echo "=== 4. GET /api/loyalty/my ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/loyalty/my | python3 -m json.tool 2>/dev/null

echo "=== 5. GET /api/missions/my/progress ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/missions/my/progress | python3 -m json.tool 2>/dev/null
