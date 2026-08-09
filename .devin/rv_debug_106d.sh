#!/bin/bash
echo "=== ShopERP SQLite: Customers (via .NET query) ==="
# Use dotnet-script or direct SQL via ShopERP API
# Since sqlite3 not in container, use a Python script with sqlite3 module
docker exec vanan-shoperp python3 -c "
import sqlite3, json
conn = sqlite3.connect('/app/vanan_shoperp.db')
conn.row_factory = sqlite3.Row
cur = conn.cursor()
print('=== Customers in SQLite ===')
for row in cur.execute('SELECT Id, FullName, PhoneNumber, Email, DeviceId, LoyaltyPoints, IdentityLevel FROM Customers ORDER BY CreatedAt DESC LIMIT 20'):
    print(dict(row))
print()
print('=== LoyaltyRewards in SQLite ===')
for row in cur.execute('SELECT Id, CustomerId, PointBalance, IsActive FROM LoyaltyRewards LIMIT 20'):
    print(dict(row))
print()
print('=== Orders with CustomerDeviceId (last 5) ===')
for row in cur.execute('SELECT Id, CustomerId, CustomerDeviceId, Status, TotalAmount FROM Orders ORDER BY CreatedAt DESC LIMIT 5'):
    print(dict(row))
conn.close()
" 2>&1
