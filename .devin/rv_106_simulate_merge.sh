#!/bin/bash
# RV: Simulate customer login with DeviceId=54A5236E to trigger merge
# Use OTP verify endpoint — in production, OTP is sent via SMS.
# We'll send OTP first, then extract it from logs (if logged) or use dev mode.

DEVICE_ID="54a5236e-c89a-49d7-aa7b-c8d2071db738"
PHONE="0901234567"  # Test phone — will create new customer if not exists

echo "=== 1. Send OTP ==="
RESP1=$(curl -sk -X POST 'https://app.khachvip.online/api/customer-identity/otp/send' \
  -H 'Content-Type: application/json' \
  -d "{\"phoneNumber\":\"$PHONE\",\"tenantId\":\"00000000-0000-0000-0000-000000000001\"}" \
  -D /tmp/otp_headers.txt 2>&1)
echo "Response: $RESP1"
echo "Headers:"
grep -i 'x-dev-otp' /tmp/otp_headers.txt 2>&1 || echo "(no X-Dev-OTP header — production mode)"

echo ""
echo "=== 2. Check ShopERP logs for OTP ==="
docker logs vanan-shoperp --since 2m 2>&1 | grep -i "otp\|OTP generated" | tail -5

echo ""
echo "=== 3. Try to extract OTP from logs (if available) ==="
OTP=$(docker logs vanan-shoperp --since 2m 2>&1 | grep -oP 'OTP for [0-9]+: \K[0-9]+' | tail -1)
if [ -z "$OTP" ]; then
  OTP=$(docker logs vanan-shoperp --since 2m 2>&1 | grep -i 'otp' | grep -oP '\b[0-9]{6}\b' | tail -1)
fi
echo "Extracted OTP: ${OTP:-none}"

if [ -n "$OTP" ]; then
  echo ""
  echo "=== 4. Verify OTP with DeviceId=54a5236e (triggers merge) ==="
  RESP2=$(curl -sk -X POST 'https://app.khachvip.online/api/customer-identity/otp/verify' \
    -H 'Content-Type: application/json' \
    -d "{\"phoneNumber\":\"$PHONE\",\"otp\":\"$OTP\",\"deviceId\":\"$DEVICE_ID\",\"tenantId\":\"00000000-0000-0000-0000-000000000001\",\"displayName\":\"RV Merge Test\"}" 2>&1)
  echo "Response: $RESP2"

  echo ""
  echo "=== 5. Check logs for merge activity ==="
  sleep 2
  docker logs vanan-shoperp --since 1m 2>&1 | grep -iE 'CustomerMerge|TD-CUSTSYNC|MergeDevice|merged|transferred' | tail -10

  echo ""
  echo "=== 6. SQLite: Check stub 405154EF after merge ==="
  docker exec vanan-shoperp sqlite3 /app/keys/vanan_shoperp.db \
    "SELECT Id, FullName, DeviceId, LoyaltyPoints, IsDeleted FROM Customers WHERE Id IN ('405154EF-B7D2-4F82-982D-8AE5C46978A0','76056D36-F3F6-4844-9562-328EE58E4B8E');" 2>&1

  echo ""
  echo "=== 7. SQLite: LoyaltyRewards after merge ==="
  docker exec vanan-shoperp sqlite3 /app/keys/vanan_shoperp.db \
    "SELECT lr.CustomerId, lr.PointBalance, c.FullName, c.DeviceId, c.IsDeleted
     FROM LoyaltyRewards lr JOIN Customers c ON lr.CustomerId = c.Id
     WHERE c.DeviceId LIKE '%54A5236E%' OR c.FullName LIKE '%Merge%';" 2>&1
else
  echo "Cannot extract OTP — production mode. Merge will trigger on real customer login."
fi
