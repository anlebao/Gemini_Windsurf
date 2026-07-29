#!/bin/bash
set +e
KHACHLINK="https://diemthuong.khachvip.online"
GATEWAY="https://api.khachvip.online"
PASS=0; FAIL=0

# 1. Pickup API no token → 401
PICKUP_CODE=$(curl -sk -o /dev/null -w "%{http_code}" -X POST $GATEWAY/api/community/orders/00000000-0000-0000-0000-000000000099/pickup)
if [ "$PICKUP_CODE" = "401" ]; then echo "[PASS] rv-s2-pickup-no-token-401 — $PICKUP_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-pickup-no-token-401 — got $PICKUP_CODE"; FAIL=$((FAIL+1)); fi

# 2. Delivering API no token → 401
DELIVERING_CODE=$(curl -sk -o /dev/null -w "%{http_code}" -X POST $GATEWAY/api/community/orders/00000000-0000-0000-0000-000000000099/delivering)
if [ "$DELIVERING_CODE" = "401" ]; then echo "[PASS] rv-s2-delivering-no-token-401 — $DELIVERING_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-delivering-no-token-401 — got $DELIVERING_CODE"; FAIL=$((FAIL+1)); fi

# 3. Delivered API no token → 401
DELIVERED_CODE=$(curl -sk -o /dev/null -w "%{http_code}" -X POST $GATEWAY/api/community/orders/00000000-0000-0000-0000-000000000099/delivered)
if [ "$DELIVERED_CODE" = "401" ]; then echo "[PASS] rv-s2-delivered-no-token-401 — $DELIVERED_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-delivered-no-token-401 — got $DELIVERED_CODE"; FAIL=$((FAIL+1)); fi

# 4. Failed API no token → 401
FAILED_CODE=$(curl -sk -o /dev/null -w "%{http_code}" -X POST $GATEWAY/api/community/orders/00000000-0000-0000-0000-000000000099/failed -H "Content-Type: application/json" -d '{"reason":"test"}')
if [ "$FAILED_CODE" = "401" ]; then echo "[PASS] rv-s2-failed-no-token-401 — $FAILED_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-failed-no-token-401 — got $FAILED_CODE"; FAIL=$((FAIL+1)); fi

# 5. Location update API no token → 401
LOC_CODE=$(curl -sk -o /dev/null -w "%{http_code}" -X POST $GATEWAY/api/community/location/update -H "Content-Type: application/json" -d '{"deliveryTaskId":"00000000-0000-0000-0000-000000000099","lat":10.8,"lng":106.7}')
if [ "$LOC_CODE" = "401" ]; then echo "[PASS] rv-s2-location-no-token-401 — $LOC_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-location-no-token-401 — got $LOC_CODE"; FAIL=$((FAIL+1)); fi

# 6. Delivery tracking page route (Blazor WASM)
DT_CODE=$(curl -sk -o /dev/null -w "%{http_code}" $KHACHLINK/community/delivery-tracking/00000000-0000-0000-0000-000000000099)
if [ "$DT_CODE" = "200" ]; then echo "[PASS] rv-s2-delivery-tracking-route-200 — $DT_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-delivery-tracking-route-200 — got $DT_CODE"; FAIL=$((FAIL+1)); fi

# 7. Order tracking page route (Blazor WASM)
OT_CODE=$(curl -sk -o /dev/null -w "%{http_code}" $KHACHLINK/order-tracking/00000000-0000-0000-0000-000000000099)
if [ "$OT_CODE" = "200" ]; then echo "[PASS] rv-s2-order-tracking-route-200 — $OT_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-order-tracking-route-200 — got $OT_CODE"; FAIL=$((FAIL+1)); fi

# 8. WASM binary — DeliveryWorkflowService
DWS_COUNT=$(docker exec vanan-khachlink grep -ac "DeliveryWorkflowService" /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null)
if [ "$DWS_COUNT" -gt 0 ] 2>/dev/null; then echo "[PASS] rv-s2-wasm-deliveryworkflowservice — count=$DWS_COUNT"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-wasm-deliveryworkflowservice — count=$DWS_COUNT"; FAIL=$((FAIL+1)); fi

# 9. WASM binary — LocationTrackingService
LTS_COUNT=$(docker exec vanan-khachlink grep -ac "LocationTrackingService" /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null)
if [ "$LTS_COUNT" -gt 0 ] 2>/dev/null; then echo "[PASS] rv-s2-wasm-locationtrackingservice — count=$LTS_COUNT"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-wasm-locationtrackingservice — count=$LTS_COUNT"; FAIL=$((FAIL+1)); fi

# 10. WASM binary — DeliveryTracking page
DTP_COUNT=$(docker exec vanan-khachlink grep -ac "DeliveryTracking" /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null)
if [ "$DTP_COUNT" -gt 0 ] 2>/dev/null; then echo "[PASS] rv-s2-wasm-deliverytracking — count=$DTP_COUNT"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-wasm-deliverytracking — count=$DTP_COUNT"; FAIL=$((FAIL+1)); fi

# 11. WASM binary — LeafletMap component
LM_COUNT=$(docker exec vanan-khachlink grep -ac "LeafletMap" /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null)
if [ "$LM_COUNT" -gt 0 ] 2>/dev/null; then echo "[PASS] rv-s2-wasm-leafletmap — count=$LM_COUNT"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-wasm-leafletmap — count=$LM_COUNT"; FAIL=$((FAIL+1)); fi

# 12. Leaflet JS + CSS vendored on KhachLink container
LEAFLET_JS=$(docker exec vanan-khachlink test -f /usr/share/nginx/html/lib/leaflet/leaflet.js && echo "1" || echo "0")
if [ "$LEAFLET_JS" = "1" ]; then echo "[PASS] rv-s2-leaflet-js-vendored"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-leaflet-js-vendored — not found"; FAIL=$((FAIL+1)); fi

LEAFLET_CSS=$(docker exec vanan-khachlink test -f /usr/share/nginx/html/lib/leaflet/leaflet.css && echo "1" || echo "0")
if [ "$LEAFLET_CSS" = "1" ]; then echo "[PASS] rv-s2-leaflet-css-vendored"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-leaflet-css-vendored — not found"; FAIL=$((FAIL+1)); fi

# 13. Leaflet JS interop file
LEAFLET_INTEROP=$(docker exec vanan-khachlink test -f /usr/share/nginx/html/js/leaflet.js && echo "1" || echo "0")
if [ "$LEAFLET_INTEROP" = "1" ]; then echo "[PASS] rv-s2-leaflet-interop-js"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-leaflet-interop-js — not found"; FAIL=$((FAIL+1)); fi

# 14. Gateway — LocationHub endpoint exists (negotiate returns 401 without token, not 404)
HUB_NEG=$(curl -sk -o /dev/null -w "%{http_code}" -X POST $GATEWAY/hubs/location/negotiate -H "Content-Type: application/json" -d '{}')
if [ "$HUB_NEG" != "404" ]; then echo "[PASS] rv-s2-locationhub-exists — $HUB_NEG (not 404)"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-locationhub-exists — 404"; FAIL=$((FAIL+1)); fi

# 15. Regression — Sprint 1 endpoints still work
ROLE_CODE=$(curl -sk -o /dev/null -w "%{http_code}" $GATEWAY/api/community/role)
if [ "$ROLE_CODE" = "401" ]; then echo "[PASS] rv-s2-regression-role-401 — $ROLE_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-regression-role-401 — got $ROLE_CODE"; FAIL=$((FAIL+1)); fi

NEARBY_CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/api/community/nearby-orders?lat=10.8&lng=106.7&radiusKm=5")
if [ "$NEARBY_CODE" = "401" ]; then echo "[PASS] rv-s2-regression-nearby-401 — $NEARBY_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-regression-nearby-401 — got $NEARBY_CODE"; FAIL=$((FAIL+1)); fi

# 16. Regression — KhachLink home + login pages
HOME_CODE=$(curl -sk -o /dev/null -w "%{http_code}" $KHACHLINK/)
if [ "$HOME_CODE" = "200" ]; then echo "[PASS] rv-s2-regression-home-200 — $HOME_CODE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-regression-home-200 — got $HOME_CODE"; FAIL=$((FAIL+1)); fi

NEARBY_PAGE=$(curl -sk -o /dev/null -w "%{http_code}" $KHACHLINK/community/nearby-orders)
if [ "$NEARBY_PAGE" = "200" ]; then echo "[PASS] rv-s2-regression-nearby-page-200 — $NEARBY_PAGE"; PASS=$((PASS+1)); else echo "[FAIL] rv-s2-regression-nearby-page-200 — got $NEARBY_PAGE"; FAIL=$((FAIL+1)); fi

echo ""
echo "========================================"
echo "CC-S2 Sprint 2 RV SUMMARY: PASS=$PASS  FAIL=$FAIL"
echo "========================================"
