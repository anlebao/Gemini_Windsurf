#!/bin/bash
set +e
KHACHLINK="https://diemthuong.khachvip.online"
GATEWAY="https://api.khachvip.online"
PASS=0; FAIL=0

# 1. Chat history API no token → 401
HIST_CODE=$(curl -sk -o /dev/null -w "%{http_code}" $GATEWAY/api/community/chat/conversations/00000000-0000-0000-0000-000000000099)
if [ "$HIST_CODE" = "401" ]; then echo "[PASS] rv-s3-chat-history-no-token-401 — $HIST_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-chat-history-no-token-401 — got $HIST_CODE"; FAIL=$((FAIL+1)); fi

# 2. Send message API no token → 401
SEND_CODE=$(curl -sk -o /dev/null -w "%{http_code}" -X POST $GATEWAY/api/community/chat/messages -H "Content-Type: application/json" -d '{"orderId":"00000000-0000-0000-0000-000000000099","content":"hello"}')
if [ "$SEND_CODE" = "401" ]; then echo "[PASS] rv-s3-send-message-no-token-401 — $SEND_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-send-message-no-token-401 — got $SEND_CODE"; FAIL=$((FAIL+1)); fi

# 3. ChatHub endpoint exists (negotiate returns non-404)
HUB_NEG=$(curl -sk -o /dev/null -w "%{http_code}" -X POST $GATEWAY/hubs/chat/negotiate -H "Content-Type: application/json" -d '{}')
if [ "$HUB_NEG" != "404" ]; then echo "[PASS] rv-s3-chathub-exists — $HUB_NEG (not 404)"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-chathub-exists — 404"; FAIL=$((FAIL+1)); fi

# 4. Delivery tracking page route (Blazor WASM) — chat panel embedded
DT_CODE=$(curl -sk -o /dev/null -w "%{http_code}" $KHACHLINK/community/delivery-tracking/00000000-0000-0000-0000-000000000099)
if [ "$DT_CODE" = "200" ]; then echo "[PASS] rv-s3-delivery-tracking-route-200 — $DT_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-delivery-tracking-route-200 — got $DT_CODE"; FAIL=$((FAIL+1)); fi

# 5. Order tracking page route (Blazor WASM) — chat panel embedded
OT_CODE=$(curl -sk -o /dev/null -w "%{http_code}" $KHACHLINK/order-tracking/00000000-0000-0000-0000-000000000099)
if [ "$OT_CODE" = "200" ]; then echo "[PASS] rv-s3-order-tracking-route-200 — $OT_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-order-tracking-route-200 — got $OT_CODE"; FAIL=$((FAIL+1)); fi

# 6. WASM binary — ChatService
CS_COUNT=$(docker exec vanan-khachlink grep -ac "ChatService" /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null)
if [ "$CS_COUNT" -gt 0 ] 2>/dev/null; then echo "[PASS] rv-s3-wasm-chatservice — count=$CS_COUNT"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-wasm-chatservice — count=$CS_COUNT"; FAIL=$((FAIL+1)); fi

# 7. WASM binary — ChatPanel component
CP_COUNT=$(docker exec vanan-khachlink grep -ac "ChatPanel" /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null)
if [ "$CP_COUNT" -gt 0 ] 2>/dev/null; then echo "[PASS] rv-s3-wasm-chatpanel — count=$CP_COUNT"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-wasm-chatpanel — count=$CP_COUNT"; FAIL=$((FAIL+1)); fi

# 8. WASM binary — ChatHttpService
CHS_COUNT=$(docker exec vanan-khachlink grep -ac "ChatHttpService" /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null)
if [ "$CHS_COUNT" -gt 0 ] 2>/dev/null; then echo "[PASS] rv-s3-wasm-chathttpservice — count=$CHS_COUNT"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-wasm-chathttpservice — count=$CHS_COUNT"; FAIL=$((FAIL+1)); fi

# 9. WASM binary — GetCustomerIdAsync (CommunityHttpService new method)
GCI_COUNT=$(docker exec vanan-khachlink grep -ac "GetCustomerIdAsync" /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null)
if [ "$GCI_COUNT" -gt 0 ] 2>/dev/null; then echo "[PASS] rv-s3-wasm-getcustomeridasync — count=$GCI_COUNT"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-wasm-getcustomeridasync — count=$GCI_COUNT"; FAIL=$((FAIL+1)); fi

# 10. Gateway DLL — ChatService (server-side, in CoreHub.dll)
GCS_COUNT=$(docker exec vanan-gateway grep -ac 'ChatService' /app/VanAn.CoreHub.dll 2>/dev/null)
if [ "$GCS_COUNT" -gt 0 ] 2>/dev/null; then echo "[PASS] rv-s3-gateway-chatservice — count=$GCS_COUNT"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-gateway-chatservice — count=$GCS_COUNT"; FAIL=$((FAIL+1)); fi

# 11. Gateway DLL — ChatHub
GCH_COUNT=$(docker exec vanan-gateway grep -ac 'ChatHub' /app/VanAn.Gateway.dll 2>/dev/null)
if [ "$GCH_COUNT" -gt 0 ] 2>/dev/null; then echo "[PASS] rv-s3-gateway-chathub — count=$GCH_COUNT"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-gateway-chathub — count=$GCH_COUNT"; FAIL=$((FAIL+1)); fi

# 12. pwa.js — scrollToBottom helper
STB_COUNT=$(docker exec vanan-khachlink grep -c "scrollToBottom" /usr/share/nginx/html/js/pwa.js 2>/dev/null)
if [ "$STB_COUNT" -gt 0 ] 2>/dev/null; then echo "[PASS] rv-s3-pwa-scrolltobottom — count=$STB_COUNT"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-pwa-scrolltobottom — count=$STB_COUNT"; FAIL=$((FAIL+1)); fi

# 13. Regression — Sprint 2 delivery endpoints still work
PICKUP_CODE=$(curl -sk -o /dev/null -w "%{http_code}" -X POST $GATEWAY/api/community/orders/00000000-0000-0000-0000-000000000099/pickup)
if [ "$PICKUP_CODE" = "401" ]; then echo "[PASS] rv-s3-regression-pickup-401 — $PICKUP_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-regression-pickup-401 — got $PICKUP_CODE"; FAIL=$((FAIL+1)); fi

LOC_CODE=$(curl -sk -o /dev/null -w "%{http_code}" -X POST $GATEWAY/api/community/location/update -H "Content-Type: application/json" -d '{"deliveryTaskId":"00000000-0000-0000-0000-000000000099","lat":10.8,"lng":106.7}')
if [ "$LOC_CODE" = "401" ]; then echo "[PASS] rv-s3-regression-location-401 — $LOC_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-regression-location-401 — got $LOC_CODE"; FAIL=$((FAIL+1)); fi

# 14. Regression — Sprint 1 endpoints still work
ROLE_CODE=$(curl -sk -o /dev/null -w "%{http_code}" $GATEWAY/api/community/role)
if [ "$ROLE_CODE" = "401" ]; then echo "[PASS] rv-s3-regression-role-401 — $ROLE_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-regression-role-401 — got $ROLE_CODE"; FAIL=$((FAIL+1)); fi

NEARBY_CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/api/community/nearby-orders?lat=10.8&lng=106.7&radiusKm=5")
if [ "$NEARBY_CODE" = "401" ]; then echo "[PASS] rv-s3-regression-nearby-401 — $NEARBY_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-regression-nearby-401 — got $NEARBY_CODE"; FAIL=$((FAIL+1)); fi

# 15. Regression — LocationHub still exists (Sprint 2)
LH_NEG=$(curl -sk -o /dev/null -w "%{http_code}" -X POST $GATEWAY/hubs/location/negotiate -H "Content-Type: application/json" -d '{}')
if [ "$LH_NEG" != "404" ]; then echo "[PASS] rv-s3-regression-locationhub-exists — $LH_NEG (not 404)"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-regression-locationhub-exists — 404"; FAIL=$((FAIL+1)); fi

# 16. Regression — KhachLink home page
HOME_CODE=$(curl -sk -o /dev/null -w "%{http_code}" $KHACHLINK/)
if [ "$HOME_CODE" = "200" ]; then echo "[PASS] rv-s3-regression-home-200 — $HOME_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s3-regression-home-200 — got $HOME_CODE"; FAIL=$((FAIL+1)); fi

echo ""
echo "========================================"
echo "CC-S3 Sprint 3 RV SUMMARY: PASS=$PASS  FAIL=$FAIL"
echo "========================================"
