#!/bin/bash
echo "=== Stub 405154EF after merge (should be IsDeleted=1) ==="
docker exec vanan-shoperp sqlite3 /app/keys/vanan_shoperp.db \
  "SELECT Id, FullName, DeviceId, IsDeleted FROM Customers WHERE Id = '405154EF-B7D2-4F82-982D-8AE5C46978A0';"

echo ""
echo "=== Login customer 764C4023 after merge (should have DeviceId=54A5236E) ==="
docker exec vanan-shoperp sqlite3 /app/keys/vanan_shoperp.db \
  "SELECT Id, FullName, PhoneNumber, DeviceId, IsDeleted FROM Customers WHERE Id = '764C4023-F1A8-4DEC-AFC7-89D272D44BCF';"

echo ""
echo "=== LoyaltyRewards for login customer (should have 95612+ points) ==="
docker exec vanan-shoperp sqlite3 /app/keys/vanan_shoperp.db \
  "SELECT CustomerId, PointBalance, IsActive FROM LoyaltyRewards WHERE CustomerId = '764C4023-F1A8-4DEC-AFC7-89D272D44BCF';"

echo ""
echo "=== LoyaltyRewards for stub (should be 0 or inactive) ==="
docker exec vanan-shoperp sqlite3 /app/keys/vanan_shoperp.db \
  "SELECT CustomerId, PointBalance, IsActive FROM LoyaltyRewards WHERE CustomerId = '405154EF-B7D2-4F82-982D-8AE5C46978A0';"
