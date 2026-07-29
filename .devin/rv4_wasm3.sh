#!/bin/bash
echo "=== KhachLink: grep -a Sprint 4 page names in VanAn.KhachLink.wasm ==="
docker exec vanan-khachlink sh -c 'grep -aoE "NearbyProducts|SalesmanQR|SalesDashboard|CommunityHttpService|GetCompositeSalesmanQrAsync|GetNearbyProductsAsync|isSalesman|vanan_referral_code" /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null | sort -u'

echo ""
echo "=== KhachLink: grep -a Sprint 4 routes in VanAn.KhachLink.wasm ==="
docker exec vanan-khachlink sh -c 'grep -aoE "community/nearby-products|community/salesman-qr|community/sales-dashboard" /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null | sort -u'

echo ""
echo "=== KhachLink: VanAn.Shared.wasm — check for Sprint 4 DTOs ==="
docker exec vanan-khachlink sh -c 'grep -aoE "CompositeSalesmanQrDto|NearbyProductDto|CommissionSummaryDto|AppInstallAttributionDto|ProductReferralConfigDto" /usr/share/nginx/html/_framework/VanAn.Shared.wasm 2>/dev/null | sort -u'

echo ""
echo "=== Gateway: full healthcheck (DB connectivity) ==="
curl -s -o /dev/null -w "Gateway /health: HTTP %{http_code}\n" "https://api.khachvip.online/health" 2>/dev/null
curl -s -o /dev/null -w "Gateway /healthz: HTTP %{http_code}\n" "https://api.khachvip.online/healthz" 2>/dev/null
curl -s -o /dev/null -w "Gateway / (root): HTTP %{http_code}\n" "https://api.khachvip.online/" 2>/dev/null

echo ""
echo "=== Gateway: ChatHub endpoint still mapped (Sprint 3 regression) ==="
curl -s -o /dev/null -w "ChatHub negotiate: HTTP %{http_code}\n" -X POST "https://api.khachvip.online/hubs/chat/negotiate" 2>/dev/null

echo ""
echo "=== Gateway: LocationHub endpoint still mapped (Sprint 2 regression) ==="
curl -s -o /dev/null -w "LocationHub negotiate: HTTP %{http_code}\n" -X POST "https://api.khachvip.online/hubs/location/negotiate" 2>/dev/null
