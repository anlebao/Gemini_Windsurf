#!/bin/bash
# Check ProductReferralConfigs.CommissionBase + TenantSettings.CommerceModeOverride
echo "=== ProductReferralConfigs.CommissionBase ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT column_name FROM information_schema.columns WHERE table_name='ProductReferralConfigs' AND column_name='CommissionBase';"
echo "=== Tenant CommerceModeOverride ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT column_name FROM information_schema.columns WHERE table_name='Tenants' AND column_name LIKE '%CommerceMode%';"
echo "=== ShopERP DLL check ==="
docker exec vanan-shoperp grep -c CommerceMode /app/VanAn.ShopERP.dll
docker exec vanan-shoperp grep -c CommunityFund /app/VanAn.ShopERP.dll
docker exec vanan-shoperp grep -c ProductCostPrice /app/VanAn.ShopERP.dll
