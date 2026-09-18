#!/usr/bin/env bash
# Realtime Platform RV — P1..P5 on production VPS (diemthuong2 / api2).
# Run from a machine with curl. Exit code = number of FAILED checks.
set -u
API="https://api2.khachvip.online"
WEB="https://diemthuong2.khachvip.online"
ORDER_C="01a0afac-f513-71d4-8719-796708cac905"      # logged-in customer order (device 81f43d82)
DEV_C="81f43d82-2486-4566-85cb-f439075c86c2"
ORDER_G="01a0b4a8-9874-7670-ad61-d17e44a25dc3"      # GUEST order (no CustomerId)
DEV_G="c9ea713e-8eb7-471b-bf7b-9e19f688c4da"
ORDER_NEW="01a0b331-1ff4-77d2-a2ff-59ad34f799a8"    # newest DELIVERY (same device)
TENANT_SHOP="7c021960-6d6c-4de4-88a1-cf8373184f49" # Cafe Tân Quy (P5 shop chat)
WRONG_DEV="00000000-0000-0000-0000-00000000dead"
FRESH_DEV=$(python -c "import uuid; print(uuid.uuid4())" 2>/dev/null || echo "11111111-2222-3333-4444-555555555555")
PASS=0; FAIL=0

check() { # check <name> <expected> <actual>
  if [ "$2" = "$3" ]; then PASS=$((PASS+1)); echo "✅ $1 (got $3)";
  else FAIL=$((FAIL+1)); echo "❌ $1 — expected $2 got $3"; fi
}

H() { curl -s -o /dev/null -w "%{http_code}" "$@"; }
B() { curl -s "$@"; }

echo "═══ P1: ORDER CHAT (legacy path — /api/community/chat/*) ═══"
# Legacy rule: device identity matches ONLY guest orders (CustomerId=null). On a customer-linked
# order the device guid ≠ conversation.CustomerId → 403 is CORRECT legacy behavior; the generic
# surface (P4) is the one that resolves device→order via Order.CustomerDeviceId (checked below).
check "P1 chat history (customer order + device, legacy 403 by design)" 403 "$(H -H "X-Customer-Device-Id: $DEV_C" "$API/api/community/chat/conversations/$ORDER_C")"
check "P1 chat history (guest device)"    200 "$(H -H "X-Customer-Device-Id: $DEV_G" "$API/api/community/chat/conversations/$ORDER_G")"
check "P1 chat history (wrong device)"    403 "$(H -H "X-Customer-Device-Id: $WRONG_DEV" "$API/api/community/chat/conversations/$ORDER_C")"
check "P1 chat history (no identity)"     401 "$(H "$API/api/community/chat/conversations/$ORDER_C")"

echo "═══ P1/P4: ORDER TRACKING (buyer map endpoint) ═══"
check "P1 tracking (customer device)"     200 "$(H -H "X-Customer-Device-Id: $DEV_C" "$API/api/community/orders/$ORDER_C/tracking")"
check "P1 tracking (guest device)"        200 "$(H -H "X-Customer-Device-Id: $DEV_G" "$API/api/community/orders/$ORDER_G/tracking")"
check "P1 tracking (wrong device)"        403 "$(H -H "X-Customer-Device-Id: $WRONG_DEV" "$API/api/community/orders/$ORDER_C/tracking")"
TRACK=$(B -H "X-Customer-Device-Id: $DEV_C" "$API/api/community/orders/$ORDER_C/tracking")
echo "$TRACK" | grep -q "shopLat" && { PASS=$((PASS+1)); echo "✅ P1 tracking payload has shopLat"; } || { FAIL=$((FAIL+1)); echo "❌ P1 tracking payload missing shopLat: ${TRACK:0:200}"; }

echo "═══ P4: GENERIC ORDER SURFACE (P4 migration + P6 fix) ═══"
check "P4 generic chat history (customer device)" 200 "$(H -H "X-Customer-Device-Id: $DEV_C" "$API/api/realtime/conversations/Order/$ORDER_C?take=20")"
check "P4 generic chat history (guest order)"     200 "$(H -H "X-Customer-Device-Id: $DEV_G" "$API/api/realtime/conversations/Order/$ORDER_G?take=20")"
check "P4 generic chat history (wrong device)"    403 "$(H -H "X-Customer-Device-Id: $WRONG_DEV" "$API/api/realtime/conversations/Order/$ORDER_C?take=20")"
check "P4 generic latest location (customer device)" 200 "$(H -H "X-Customer-Device-Id: $DEV_C" "$API/api/realtime/location/Order/$ORDER_NEW/latest")"
MSG="RV-P1P5 $(date +%s)"
SEND=$(B -X POST -H "X-Customer-Device-Id: $DEV_G" -H "Content-Type: application/json" \
  -d "{\"subjectType\":\"Order\",\"subjectId\":\"$ORDER_G\",\"content\":\"$MSG\"}" "$API/api/realtime/conversations/messages")
echo "$SEND" | grep -q "messageId" && { PASS=$((PASS+1)); echo "✅ P4 generic send (guest order) 200 + messageId"; } || { FAIL=$((FAIL+1)); echo "❌ P4 generic send failed: $SEND"; }
HIST=$(B -H "X-Customer-Device-Id: $DEV_G" "$API/api/realtime/conversations/Order/$ORDER_G?take=5")
echo "$HIST" | grep -q "$MSG" && { PASS=$((PASS+1)); echo "✅ P4 generic history round-trip (message persisted)"; } || { FAIL=$((FAIL+1)); echo "❌ P4 generic history missing sent message"; }

echo "═══ P5: SHOP CHAT ═══"
check "P5 shop conversations (no identity)" 401 "$(H "$API/api/realtime/shop/conversations")"
check "P5 shop history (fresh device creates)" 200 "$(H -H "X-Customer-Device-Id: $FRESH_DEV" "$API/api/realtime/conversations/Shop/$TENANT_SHOP?take=10")"
SEND_SHOP=$(B -X POST -H "X-Customer-Device-Id: $FRESH_DEV" -H "Content-Type: application/json" \
  -d "{\"subjectType\":\"Shop\",\"subjectId\":\"$TENANT_SHOP\",\"content\":\"RV-P5 $MSG\"}" "$API/api/realtime/conversations/messages")
echo "$SEND_SHOP" | grep -q "messageId" && { PASS=$((PASS+1)); echo "✅ P5 shop send (fresh device) 200"; } || { FAIL=$((FAIL+1)); echo "❌ P5 shop send failed: $SEND_SHOP"; }
OTHER_DEV=$(python -c "import uuid; print(uuid.uuid4())" 2>/dev/null || echo "99999999-9999-9999-9999-999999999999")
OHIST=$(B -H "X-Customer-Device-Id: $OTHER_DEV" "$API/api/realtime/conversations/Shop/$TENANT_SHOP?take=10")
echo "$OHIST" | grep -q "RV-P5 $MSG" && { FAIL=$((FAIL+1)); echo "❌ P5 privacy — other device sees first device's message!"; } || { PASS=$((PASS+1)); echo "✅ P5 privacy — other device does NOT see the message"; }

echo "═══ P3: HUBS (negotiate not 404) ═══"
for hub in chat location messaging tracking; do
  code=$(curl -s -o /dev/null -w "%{http_code}" -X POST -H "Content-Type: application/json" -d '{}' "$API/hubs/$hub/negotiate")
  if [ "$code" != "404" ]; then PASS=$((PASS+1)); echo "✅ hub /hubs/$hub exists ($code)"; else FAIL=$((FAIL+1)); echo "❌ hub /hubs/$hub 404"; fi
done

echo "═══ L2: RCL STATIC ASSETS (F4) ═══"
check "L2 realtime.js (KhachLink)"   200 "$(H "$WEB/_content/VanAn.UI.Platform/js/realtime.js")"
check "L2 realtime.js (ShopERP)"     200 "$(H "$API/shoperp/_content/VanAn.UI.Platform/js/realtime.js")"
check "L2 realtime.js (Directory)"   200 "$(H "https://timlathay.com/_content/VanAn.UI.Platform/js/realtime.js")"
check "L2 leaflet.css (KhachLink)"   200 "$(H "$WEB/_content/VanAn.UI.Platform/lib/leaflet/leaflet.css")"

echo ""
echo "═══════════════════════════════════"
echo "RESULT: $PASS passed, $FAIL failed"
exit $FAIL
