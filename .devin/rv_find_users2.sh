#!/bin/bash
echo "=== Users table ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c 'SELECT "Username", "Email", "Role" FROM "Users" LIMIT 10;' 2>&1
echo ""
echo "=== Tenants ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c 'SELECT "Id", "Name" FROM "Tenants" LIMIT 5;' 2>&1
