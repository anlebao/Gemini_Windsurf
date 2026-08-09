#!/bin/bash
TENANT="00000000-0000-0000-0000-000000000001"
ORDER_ID="019fd240-375f-75ef-a0ec-08ee5bb18d48"

echo "=== 1. All loyalty-related tables in PG ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "\dt *loyalty* \dt *Loyalty*" 2>&1
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "\dt *Reward* \dt *History*" 2>&1

echo ""
echo "=== 2. ShopFeatureSettings ALL tenants ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
  "SELECT \"TenantId\", \"Loyalty_Program_Enabled\", \"Loyalty_PointsRate\" FROM \"ShopFeatureSettings\" LIMIT 10;" 2>&1

echo ""
echo "=== 3. LoyaltyRewards ALL tenants ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
  "SELECT \"Id\", \"TenantId\", \"PointBalance\", \"IsActive\" FROM \"LoyaltyRewards\" LIMIT 10;" 2>&1

echo ""
echo "=== 4. LoyaltyPointHistories table (correct name?) ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "\dt *Point*" 2>&1
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
  "SELECT table_name FROM information_schema.tables WHERE table_name ILIKE '%loyalty%' OR table_name ILIKE '%point%' OR table_name ILIKE '%history%' OR table_name ILIKE '%reward%';" 2>&1

echo ""
echo "=== 5. Order CustomerId + Customer info ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
  "SELECT \"Id\", \"CustomerId\", \"CustomerInfo\", \"Status\", \"PaymentStatus\" FROM \"Orders\" WHERE \"Id\" = '$ORDER_ID';" 2>&1

echo ""
echo "=== 6. Customers for this tenant ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
  "SELECT \"Id\", \"FullName\", \"PhoneNumber\", \"LoyaltyPoints\", \"TenantId\" FROM \"Customers\" WHERE \"TenantId\" = '$TENANT' LIMIT 10;" 2>&1
