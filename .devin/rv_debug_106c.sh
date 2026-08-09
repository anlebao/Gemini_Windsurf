#!/bin/bash
TENANT="00000000-0000-0000-0000-000000000001"
ORDER_ID="019fd240-375f-75ef-a0ec-08ee5bb18d48"

echo "=== 1. PG: All customers for tenant (identity fields) ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
  "SELECT \"Id\", \"FullName\", \"PhoneNumber\", \"Email\", \"DeviceId\", \"LoyaltyPoints\", \"IdentityLevel\", \"CreatedAt\"
   FROM \"Customers\" WHERE \"TenantId\" = '$TENANT' ORDER BY \"CreatedAt\" DESC;" 2>&1

echo ""
echo "=== 2. PG: Order details (CustomerId, CustomerDeviceId, CustomerInfo) ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
  "SELECT \"Id\", \"CustomerId\", \"CustomerDeviceId\", \"Status\", \"PaymentStatus\", \"TotalAmount\", \"CreatedAt\"
   FROM \"Orders\" WHERE \"Id\" = '$ORDER_ID';" 2>&1

echo ""
echo "=== 3. PG: All orders for this tenant (last 5) — check CustomerId vs CustomerDeviceId ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
  "SELECT \"Id\", \"CustomerId\", \"CustomerDeviceId\", \"Status\", \"TotalAmount\", \"CreatedAt\"
   FROM \"Orders\" WHERE \"TenantId\" = '$TENANT' ORDER BY \"CreatedAt\" DESC LIMIT 5;" 2>&1

echo ""
echo "=== 4. PG: LoyaltyRewards (all) — check which customer has points ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
  "SELECT \"Id\", \"TenantId\", \"PointBalance\", \"IsActive\", \"CreatedAt\" FROM \"LoyaltyRewards\";" 2>&1

echo ""
echo "=== 5. PG: LoyaltyRewards schema — check if CustomerId column exists ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c '\d "LoyaltyRewards"' 2>&1

echo ""
echo "=== 6. ShopERP SQLite: Customers (via dotnet exec or API) ==="
# Can't use sqlite3 directly — use API instead
curl -sk 'https://app.khachvip.online/api/loyalty/my' -H 'X-Customer-Token: invalid' 2>&1 | head -1
