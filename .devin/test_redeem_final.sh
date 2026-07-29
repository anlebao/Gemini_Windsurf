#!/bin/bash
# Full redeem test: OTP login → check balance → redeem → verify voucher
echo "=== 1. Send OTP ==="
curl -sk -D /tmp/rf_headers -X POST -H 'Content-Type: application/json' \
  -d '{"phoneNumber":"0900000055"}' \
  https://api.khachvip.online/api/customers/otp/send
echo
OTP=$(grep -i 'X-Dev-OTP' /tmp/rf_headers | tr -d '\r' | awk '{print $2}')
echo "OTP: $OTP"

echo "=== 2. Verify OTP ==="
VERIFY=$(curl -sk -X POST -H 'Content-Type: application/json' \
  -d "{\"phoneNumber\":\"0900000055\",\"otp\":\"$OTP\"}" \
  https://api.khachvip.online/api/customers/otp/verify)
echo "$VERIFY" | python3 -m json.tool 2>/dev/null
TOKEN=$(echo "$VERIFY" | python3 -c "import sys,json; print(json.load(sys.stdin)['customerToken'])" 2>/dev/null)
echo "Token: ${TOKEN:0:30}..."

echo "=== 3. GET /api/loyalty/my (expect 300 points) ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/loyalty/my | python3 -m json.tool 2>/dev/null

echo "=== 4. POST /api/redemption/redeem (Ca phe mien phi, 200 pts) ==="
curl -sk -X POST -H 'Content-Type: application/json' \
  -H "X-Customer-Token: $TOKEN" \
  -d '{"CatalogItemId":"8bcc833e-51c3-4508-adfb-41e2ff96ff79"}' \
  https://api.khachvip.online/api/redemption/redeem | python3 -m json.tool 2>/dev/null

echo "=== 5. GET /api/loyalty/my (expect 100 points after redeem) ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/loyalty/my | python3 -m json.tool 2>/dev/null

echo "=== 6. GET /api/redemption/my/vouchers ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/redemption/my/vouchers | python3 -m json.tool 2>/dev/null

echo "=== 7. GET /api/redemption/my/redemptions ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/redemption/my/redemptions | python3 -m json.tool 2>/dev/null
