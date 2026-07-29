#!/bin/bash
echo "=== Gateway: /app DLL listing (top-level) ==="
docker exec vanan-gateway ls /app 2>&1 | head -40

echo ""
echo "=== Gateway: find VanAn.CoreHub.dll ==="
docker exec vanan-gateway find /app -name "VanAn.CoreHub.dll" 2>/dev/null

echo ""
echo "=== Gateway: find VanAn.Gateway.dll ==="
docker exec vanan-gateway find /app -name "VanAn.Gateway.dll" 2>/dev/null

echo ""
echo "=== Gateway: strings in VanAn.CoreHub.dll for Sprint 4 services ==="
docker exec vanan-gateway sh -c 'COREHUB=$(find /app -name "VanAn.CoreHub.dll" | head -1); echo "Using: $COREHUB"; strings "$COREHUB" 2>/dev/null | grep -E "SalesmanService|AppInstallAttributionService|FraudFlagService|ProductReferralConfigService|CoolingPeriodJob|HeldTimeoutJob" | sort -u'

echo ""
echo "=== Gateway: strings in VanAn.Gateway.dll for Sprint 4 routes ==="
docker exec vanan-gateway sh -c 'GW=$(find /app -name "VanAn.Gateway.dll" | head -1); echo "Using: $GW"; strings "$GW" 2>/dev/null | grep -E "nearby-products|salesman/qr|salesman/commissions|app-install/attributed|resolve-referral|referral-configs|ProductReferralConfigController" | sort -u'

echo ""
echo "=== ShopERP: find VanAn.ShopERP.dll ==="
docker exec vanan-shoperp find /app -name "VanAn.ShopERP.dll" 2>/dev/null | head -3

echo ""
echo "=== ShopERP: strings for ProductReferralConfigs page ==="
docker exec vanan-shoperp sh -c 'SE=$(find /app -name "VanAn.ShopERP.dll" | head -1); echo "Using: $SE"; strings "$SE" 2>/dev/null | grep -E "ProductReferralConfigs|admin/product-referral-configs" | sort -u'

echo ""
echo "=== KhachLink: find VanAn.KhachLink.dll ==="
docker exec vanan-khachlink find /app/wwwroot/_framework -name "VanAn.KhachLink.dll" 2>/dev/null | head -3

echo ""
echo "=== KhachLink: strings for Sprint 4 pages ==="
docker exec vanan-khachlink sh -c 'KL=$(find /app/wwwroot/_framework -name "VanAn.KhachLink.dll" | head -1); echo "Using: $KL"; strings "$KL" 2>/dev/null | grep -E "NearbyProducts|SalesmanQR|SalesDashboard|CommunityHttpService|GetCompositeSalesmanQr|GetNearbyProducts" | sort -u'
