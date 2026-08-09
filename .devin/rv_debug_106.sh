#!/bin/bash
TENANT="00000000-0000-0000-0000-000000000001"
ORDER_ID="019fd240-375f-75ef-a0ec-08ee5bb18d48"

echo "=== 1. Tenant feature settings (Loyalty_Program_Enabled) ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
  "SELECT \"Loyalty_Program_Enabled\", \"Loyalty_PointsRate\", \"Loyalty_MinPointsPerOrder\", \"Loyalty_MaxPointsPerOrder\", \"Loyalty_AwardOnAllOrders\" FROM \"ShopFeatureSettings\" WHERE \"TenantId\" = '$TENANT';" 2>&1

echo ""
echo "=== 2. LoyaltyRewards for this tenant (customer balances) ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
  "SELECT \"Id\", \"TenantId\", \"PointBalance\", \"IsActive\", \"CreatedAt\" FROM \"LoyaltyRewards\" WHERE \"TenantId\" = '$TENANT' LIMIT 10;" 2>&1

echo ""
echo "=== 3. Loyalty history for this order ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
  "SELECT * FROM \"LoyaltyPointHistory\" WHERE \"OrderId\" = '$ORDER_ID' LIMIT 5;" 2>&1

echo ""
echo "=== 4. All LoyaltyPointHistory for this tenant (last 10) ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
  "SELECT \"Id\", \"CustomerId\", \"OrderId\", \"EventType\", \"Points\", \"CreatedAt\" FROM \"LoyaltyPointHistory\" WHERE \"TenantId\" = '$TENANT' ORDER BY \"CreatedAt\" DESC LIMIT 10;" 2>&1

echo ""
echo "=== 5. Order details (CustomerId, Status) ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
  "SELECT \"Id\", \"CustomerId\", \"Status\", \"PaymentStatus\", \"CreatedAt\" FROM \"Orders\" WHERE \"Id\" = '$ORDER_ID';" 2>&1

echo ""
echo "=== 6. Check if OrderWorkflowService logged anything for this order ==="
docker logs vanan-shoperp --since 24h 2>&1 | grep -i "$ORDER_ID\|loyalty\|ProcessLoyalty" | tail -20
