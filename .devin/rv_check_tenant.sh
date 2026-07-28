#!/bin/bash
rm -f /tmp/rvL2.db*
docker cp vanan-shoperp:/app/keys/vanan_shoperp.db /tmp/rvL2.db
docker cp vanan-shoperp:/app/keys/vanan_shoperp.db-wal /tmp/rvL2.db-wal
docker cp vanan-shoperp:/app/keys/vanan_shoperp.db-shm /tmp/rvL2.db-shm

sqlite3 /tmp/rvL2.db <<'SQL'
SELECT Id, Status, TenantId, CustomerDeviceId, CustomerId FROM Orders WHERE Id = '019FA22A-EADE-7194-A248-3D01328345E0';
SELECT '---ALL TENANTS---';
SELECT Id, Name FROM Tenants;
SQL
