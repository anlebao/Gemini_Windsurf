#!/usr/bin/env bash
# P9 RV — Tingee Dynamic QR sweep. Run ON shop-a VPS (has ShopERP container + network).
# Layers: [1] route probe → [2] find/create pending topup → [3] POST /qr → verify payload+billId
set -u
BASE="https://app2.khachvip.online/admin/metering/api"

# --- caller creds: read from the running ShopERP container env ---
C=$(sudo docker ps --format '{{.Names}}' | grep -i shoperp | head -1)
[ -z "$C" ] && { echo "FAIL: no shoperp container"; exit 1; }
CID=$(sudo docker exec "$C" printenv Metering__CallerId)
CKEY=$(sudo docker exec "$C" printenv Metering__CallerKey)
[ -z "$CID" ] || [ -z "$CKEY" ] && { echo "FAIL: metering creds empty"; exit 1; }
AUTH=(-H "X-Caller-Id: $CID" -H "X-Metering-Key: $CKEY" -H "Content-Type: application/json")
echo "caller=$CID container=$C"

echo "=== [1] route probe (expect 401/404, not 5xx) ==="
curl -s -o /dev/null -w "POST /qr no-auth: %{http_code}\n" -X POST "$BASE/v1/topups/00000000-0000-0000-0000-000000000000/qr"

echo "=== [2] find pending topup (reuse existing, else create) ==="
LIST=$(curl -s "${AUTH[@]}" "$BASE/v1/topups?status=pending")
TOPUP_ID=$(echo "$LIST" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
TENANT_ID=$(echo "$LIST" | grep -o '"tenantId":"[^"]*"' | head -1 | cut -d'"' -f4)
if [ -z "$TOPUP_ID" ]; then
  echo "no pending topup → creating one (tenant=$TENANT_ID)"
  RESP=$(curl -s "${AUTH[@]}" -X POST "$BASE/v1/topups" -d "{\"tenantId\":\"$TENANT_ID\",\"amountVnd\":10000}")
  TOPUP_ID=$(echo "$RESP" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
fi
[ -z "$TOPUP_ID" ] && { echo "FAIL: no topup id. list=$LIST" | head -c 500; exit 1; }
echo "topup=$TOPUP_ID tenant=$TENANT_ID"

echo "=== [3] POST /v1/topups/{id}/qr ==="
QR=$(curl -s "${AUTH[@]}" -X POST "$BASE/v1/topups/$TOPUP_ID/qr")
echo "$QR" | head -c 800; echo
echo "$QR" | grep -q '"qrPayload":"0002' && echo "PASS: EMV VietQR payload" || echo "CHECK: qrPayload prefix"
echo "$QR" | grep -q '"billId":"QRD' && echo "PASS: billId QRD*" || echo "CHECK: billId"
echo "=== done — verify wallet credit after real transfer ==="
