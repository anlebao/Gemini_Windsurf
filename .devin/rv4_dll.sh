#!/bin/bash
echo "=== Gateway DLL: SalesmanService ==="
docker exec vanan-gateway sh -c 'ls -la /app/SalesmanService.dll /app/SalesmanService.pdb 2>/dev/null || find /app -name "SalesmanService.dll" 2>/dev/null | head -3'

echo "=== Gateway DLL: AppInstallAttributionService ==="
docker exec vanan-gateway sh -c 'find /app -name "AppInstallAttributionService.dll" 2>/dev/null | head -3'

echo "=== Gateway DLL: FraudFlagService ==="
docker exec vanan-gateway sh -c 'find /app -name "FraudFlagService.dll" 2>/dev/null | head -3'

echo "=== Gateway DLL: ProductReferralConfigService ==="
docker exec vanan-gateway sh -c 'find /app -name "ProductReferralConfigService.dll" 2>/dev/null | head -3'

echo "=== Gateway DLL: CoolingPeriodJob ==="
docker exec vanan-gateway sh -c 'find /app -name "CoolingPeriodJob.dll" 2>/dev/null | head -3'

echo "=== Gateway: types in VanAn.CoreHub.dll (verify services compiled in) ==="
docker exec vanan-gateway sh -c 'strings /app/VanAn.CoreHub.dll 2>/dev/null | grep -E "SalesmanService|AppInstallAttributionService|FraudFlagService|ProductReferralConfigService|CoolingPeriodJob|HeldTimeoutJob" | sort -u | head -20'

echo "=== Gateway: CommunityController endpoints (verify routes compiled) ==="
docker exec vanan-gateway sh -c 'strings /app/VanAn.Gateway.dll 2>/dev/null | grep -E "nearby-products|salesman/qr|salesman/commissions|app-install/attributed|resolve-referral|referral-configs" | sort -u | head -20'

echo "=== ShopERP Admin page DLL: ProductReferralConfigs ==="
docker exec vanan-shoperp sh -c 'find /app -name "*.dll" 2>/dev/null | xargs -I{} sh -c "strings {} 2>/dev/null | grep -l ProductReferralConfigs 2>/dev/null && echo FOUND_IN:{}" 2>/dev/null | head -5'
docker exec vanan-shoperp sh -c 'strings /app/VanAn.ShopERP.dll 2>/dev/null | grep -E "ProductReferralConfigs|admin/product-referral-configs" | sort -u | head -10'

echo "=== KhachLink WASM: verify salesman pages compiled ==="
docker exec vanan-khachlink sh -c 'find /app/wwwroot/_framework -name "VanAn.KhachLink.dll" 2>/dev/null | head -1'
docker exec vanan-khachlink sh -c 'strings /app/wwwroot/_framework/VanAn.KhachLink.dll 2>/dev/null | grep -E "NearbyProducts|SalesmanQR|SalesDashboard|CommunityHttpService" | sort -u | head -10'
