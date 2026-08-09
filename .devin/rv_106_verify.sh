#!/bin/bash
TENANT="00000000-0000-0000-0000-000000000001"
ORDER_ID="019fd240-375f-75ef-a0ec-08ee5bb18d48"
DEVICE_ID="54a5236e-c89a-49d7-aa7b-c8d2071db738"

echo "=== 1. PG: Customer with DeviceId 54a5236e... (the stub that got points) ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
  "SELECT \"Id\", \"FullName\", \"PhoneNumber\", \"DeviceId\", \"LoyaltyPoints\", \"IsDeleted\"
   FROM \"Customers\" WHERE \"DeviceId\"::text LIKE '%54a5236e%' OR \"Id\"::text LIKE '%03ea2ec1%';" 2>&1

echo ""
echo "=== 2. SQLite: Customer stubs with DeviceId matching order ==="
docker exec vanan-shoperp sqlite3 /app/keys/vanan_shoperp.db \
  "SELECT Id, FullName, PhoneNumber, DeviceId, LoyaltyPoints, IsDeleted FROM Customers WHERE DeviceId LIKE '%54a5236e%' OR DeviceId LIKE '%5B471DDE%';" 2>&1

echo ""
echo "=== 3. SQLite: LoyaltyRewards for those customers ==="
docker exec vanan-shoperp sqlite3 /app/keys/vanan_shoperp.db \
  "SELECT lr.Id, lr.CustomerId, lr.PointBalance, lr.IsActive, c.FullName, c.DeviceId
   FROM LoyaltyRewards lr
   JOIN Customers c ON lr.CustomerId = c.Id
   WHERE c.DeviceId LIKE '%54a5236e%' OR c.FullName LIKE '%Loyalty Tester%';" 2>&1

echo ""
echo "=== 4. ShopERP logs: CustomerMerge or TD-CUSTSYNC ==="
docker logs vanan-shoperp --since 15m 2>&1 | grep -iE 'CustomerMerge|TD-CUSTSYNC|MergeDevice' | tail -10

echo ""
echo "=== 5. ShopERP logs: Loyalty award for order 019fd240 ==="
docker logs vanan-shoperp --since 60m 2>&1 | grep -i '019fd240' | tail -5
