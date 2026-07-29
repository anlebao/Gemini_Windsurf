#!/bin/bash
# RV (Release Verification) — full customer journey test
PHONE="0900000066"
echo "============================================"
echo "RV: Full customer journey on VPS"
echo "============================================"

echo ""
echo "=== 1. Health checks ==="
for svc in shoperp gateway khachlink; do
  status=$(docker inspect --format='{{.State.Health.Status}}' vanan-$svc 2>/dev/null || echo "n/a")
  echo "  vanan-$svc: $status"
done

echo ""
echo "=== 2. Send OTP to $PHONE ==="
curl -sk -D /tmp/rv_headers -X POST -H 'Content-Type: application/json' \
  -d "{\"phoneNumber\":\"$PHONE\"}" \
  https://api.khachvip.online/api/customers/otp/send
echo
OTP=$(grep -i 'X-Dev-OTP' /tmp/rv_headers | tr -d '\r' | awk '{print $2}')
if [ -z "$OTP" ]; then echo "FAIL: No OTP in header"; exit 1; fi
echo "  OTP: $OTP"

echo ""
echo "=== 3. Verify OTP (expect pointBalance: 100 from OtpVerify mission) ==="
VERIFY=$(curl -sk -X POST -H 'Content-Type: application/json' \
  -d "{\"phoneNumber\":\"$PHONE\",\"otp\":\"$OTP\",\"DisplayName\":\"RV Test Customer\"}" \
  https://api.khachvip.online/api/customers/otp/verify)
echo "$VERIFY" | python3 -m json.tool 2>/dev/null
TOKEN=$(echo "$VERIFY" | python3 -c "import sys,json; print(json.load(sys.stdin)['customerToken'])" 2>/dev/null)
PB=$(echo "$VERIFY" | python3 -c "import sys,json; print(json.load(sys.stdin)['pointBalance'])" 2>/dev/null)
echo "  Token: ${TOKEN:0:30}..."
echo "  pointBalance: $PB"
[ "$PB" = "100" ] && echo "  PASS: OTP mission awarded 100 points" || echo "  WARN: expected 100, got $PB"

echo ""
echo "=== 4. GET /api/customers/me ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/customers/me | python3 -m json.tool 2>/dev/null

echo ""
echo "=== 5. GET /api/loyalty/my ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/loyalty/my | python3 -m json.tool 2>/dev/null

echo ""
echo "=== 6. GET /api/redemption/catalog/active ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/redemption/catalog/active | python3 -c "import sys,json; d=json.load(sys.stdin); print(f'  {len(d)} catalog items'); [print(f'  - {i[\"productName\"]}: {i[\"pointsRequired\"]} pts') for i in d]" 2>/dev/null

echo ""
echo "=== 7. POST /api/redemption/redeem (Ca phe mien phi, 200 pts — need 200+) ==="
# Add 200 points first via SQL (test only)
docker stop vanan-shoperp >/dev/null 2>&1
docker cp vanan-shoperp:/app/keys/vanan_shoperp.db /tmp/rv.db
docker cp vanan-shoperp:/app/keys/vanan_shoperp.db-wal /tmp/rv.db-wal 2>/dev/null
sqlite3 /tmp/rv.db "PRAGMA wal_checkpoint(TRUNCATE);" >/dev/null 2>&1
CUST_ID=$(echo "$VERIFY" | python3 -c "import sys,json; print(json.load(sys.stdin)['customerId'])" 2>/dev/null)
sqlite3 /tmp/rv.db "UPDATE LoyaltyRewards SET PointBalance = PointBalance + 200 WHERE CustomerId = '$CUST_ID';" 2>&1
docker cp /tmp/rv.db vanan-shoperp:/app/keys/vanan_shoperp.db
docker exec vanan-shoperp rm -f /app/keys/vanan_shoperp.db-wal /app/keys/vanan_shoperp.db-shm 2>/dev/null
docker start vanan-shoperp >/dev/null 2>&1
sleep 15

REDEEM=$(curl -sk -X POST -H 'Content-Type: application/json' \
  -H "X-Customer-Token: $TOKEN" \
  -d '{"CatalogItemId":"8bcc833e-51c3-4508-adfb-41e2ff96ff79"}' \
  https://api.khachvip.online/api/redemption/redeem)
echo "$REDEEM" | python3 -m json.tool 2>/dev/null
SUCCESS=$(echo "$REDEEM" | python3 -c "import sys,json; print(json.load(sys.stdin).get('success','false'))" 2>/dev/null)
[ "$SUCCESS" = "True" ] && echo "  PASS: Redeem success" || echo "  FAIL: Redeem failed"

echo ""
echo "=== 8. GET /api/redemption/my/vouchers ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/redemption/my/vouchers | python3 -c "import sys,json; d=json.load(sys.stdin); print(f'  {len(d)} vouchers'); [print(f'  - {v[\"voucherCode\"]} status={v[\"status\"]} valid={v[\"isValid\"]}') for v in d]" 2>/dev/null

echo ""
echo "=== 9. GET /api/missions/my/progress ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/missions/my/progress | python3 -m json.tool 2>/dev/null

echo ""
echo "=== 10. Final balance check ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/loyalty/my | python3 -c "import sys,json; d=json.load(sys.stdin); print(f'  pointBalance: {d[\"pointBalance\"]}'); print(f'  tier: {d[\"tier\"]}'); print(f'  history entries: {len(d[\"history\"])}')" 2>/dev/null

echo ""
echo "============================================"
echo "RV COMPLETE"
echo "============================================"
