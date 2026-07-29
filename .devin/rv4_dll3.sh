#!/bin/bash
echo "=== Check if strings exists in containers ==="
docker exec vanan-gateway which strings 2>&1
docker exec vanan-gateway sh -c 'command -v strings; apk info 2>/dev/null | grep -i binutils' 2>&1 | head -5

echo ""
echo "=== Gateway: use grep -a (binary-safe) on VanAn.CoreHub.dll ==="
docker exec vanan-gateway sh -c 'grep -aoE "SalesmanService|AppInstallAttributionService|FraudFlagService|ProductReferralConfigService|CoolingPeriodJob|HeldTimeoutJob" /app/VanAn.CoreHub.dll 2>/dev/null | sort -u'

echo ""
echo "=== Gateway: use grep -a on VanAn.Gateway.dll for routes ==="
docker exec vanan-gateway sh -c 'grep -aoE "nearby-products|salesman/qr|salesman/commissions|app-install/attributed|resolve-referral|referral-configs|ProductReferralConfigController" /app/VanAn.Gateway.dll 2>/dev/null | sort -u'

echo ""
echo "=== ShopERP: use grep -a on VanAn.ShopERP.dll ==="
docker exec vanan-shoperp sh -c 'grep -aoE "ProductReferralConfigs|admin/product-referral-configs" /app/VanAn.ShopERP.dll 2>/dev/null | sort -u'

echo ""
echo "=== KhachLink: locate WASM dlls ==="
docker exec vanan-khachlink sh -c 'ls /app 2>&1 | head -20'
docker exec vanan-khachlink sh -c 'find /app -name "VanAn.KhachLink.dll" 2>/dev/null | head -5'
docker exec vanan-khachlink sh -c 'find / -name "VanAn.KhachLink.dll" 2>/dev/null | head -5'

echo ""
echo "=== KhachLink: nginx serve root ==="
docker exec vanan-khachlink sh -c 'cat /etc/nginx/conf.d/default.conf 2>/dev/null | head -30'
