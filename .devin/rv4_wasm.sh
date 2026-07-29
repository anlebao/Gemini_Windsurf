#!/bin/bash
echo "=== KhachLink: WASM dll location ==="
docker exec vanan-khachlink sh -c 'ls /usr/share/nginx/html/_framework/*.dll 2>/dev/null | head -20'

echo ""
echo "=== KhachLink: VanAn.KhachLink.dll location ==="
docker exec vanan-khachlink sh -c 'find /usr/share/nginx/html -name "VanAn.KhachLink.dll" 2>/dev/null'

echo ""
echo "=== KhachLink: grep -a for Sprint 4 pages in WASM dll ==="
docker exec vanan-khachlink sh -c 'KL=$(find /usr/share/nginx/html -name "VanAn.KhachLink.dll" | head -1); echo "Using: $KL"; grep -aoE "NearbyProducts|SalesmanQR|SalesDashboard|CommunityHttpService|GetCompositeSalesmanQr|GetNearbyProducts|isSalesman" "$KL" 2>/dev/null | sort -u'

echo ""
echo "=== KhachLink: verify qrcode.js + app-install-tracker.js served from nginx ==="
docker exec vanan-khachlink sh -c 'ls -la /usr/share/nginx/html/js/qrcode.js /usr/share/nginx/html/js/app-install-tracker.js 2>&1'

echo ""
echo "=== KhachLink: index.html references (verify script tags injected) ==="
docker exec vanan-khachlink sh -c 'grep -E "qrcode|app-install-tracker" /usr/share/nginx/html/index.html 2>/dev/null'

echo ""
echo "=== Gateway logs: any Sprint 4 service startup errors? (last 100 lines) ==="
docker logs vanan-gateway --since 1h 2>&1 | grep -iE "error|exception|fail" | grep -iE "salesman|appinstall|fraud|referral|cooling|heldtimeout" | head -20

echo ""
echo "=== Gateway logs: hosted service registration (CoolingPeriodJob + HeldTimeoutJob) ==="
docker logs vanan-gateway --since 1h 2>&1 | grep -iE "CoolingPeriodJob|HeldTimeoutJob|SalesmanService|FraudFlagService" | head -10
