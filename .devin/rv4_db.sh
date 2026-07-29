#!/bin/bash
echo "=== Gateway PG: Sprint 4 tables exist ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "\dt" 2>&1 | grep -iE "ProductReferralConfig|AppInstallAttribution|DeviceRegistration|FraudFlag|SalesReferral|CommunityRole|DeliveryTask|Conversation|Message" | head -20

echo ""
echo "=== Gateway PG: count rows in Sprint 4 tables (empty is OK — just verify tables queryable) ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "SELECT 'ProductReferralConfig' as tbl, count(*) FROM \"ProductReferralConfigs\" UNION ALL SELECT 'AppInstallAttribution', count(*) FROM \"AppInstallAttributions\" UNION ALL SELECT 'DeviceRegistration', count(*) FROM \"DeviceRegistrations\" UNION ALL SELECT 'FraudFlag', count(*) FROM \"FraudFlags\" UNION ALL SELECT 'SalesReferral', count(*) FROM \"SalesReferrals\" UNION ALL SELECT 'CommunityRole', count(*) FROM \"CommunityRoles\";" 2>&1 | head -20

echo ""
echo "=== Gateway PG: CommunityRole type distribution (verify Salesman role type exists) ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "SELECT \"RoleType\", count(*) FROM \"CommunityRoles\" GROUP BY \"RoleType\";" 2>&1 | head -10
