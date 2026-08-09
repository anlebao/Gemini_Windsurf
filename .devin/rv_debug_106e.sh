#!/bin/bash
echo "=== Install sqlite3 in ShopERP container (temporary) ==="
docker exec vanan-shoperp apt-get update -qq 2>&1 | tail -1
docker exec vanan-shoperp apt-get install -y -qq sqlite3 2>&1 | tail -3
echo ""
echo "=== Customers in SQLite ==="
docker exec vanan-shoperp sqlite3 /app/vanan_shoperp.db "SELECT Id, FullName, PhoneNumber, Email, DeviceId, LoyaltyPoints, IdentityLevel FROM Customers ORDER BY CreatedAt DESC LIMIT 20;" 2>&1
echo ""
echo "=== LoyaltyRewards in SQLite ==="
docker exec vanan-shoperp sqlite3 /app/vanan_shoperp.db "SELECT Id, CustomerId, PointBalance, IsActive FROM LoyaltyRewards LIMIT 20;" 2>&1
echo ""
echo "=== Orders with CustomerDeviceId (last 5) ==="
docker exec vanan-shoperp sqlite3 /app/vanan_shoperp.db "SELECT Id, CustomerId, CustomerDeviceId, Status, TotalAmount FROM Orders ORDER BY CreatedAt DESC LIMIT 5;" 2>&1
