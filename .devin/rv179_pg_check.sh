#!/bin/bash
# Check Gateway PG for seeded products (did the ShopERP quick-setup seed go to PG?)
TID=$1
echo "=== PG Products count for tenant $TID ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT COUNT(*) FROM \"Products\" WHERE \"TenantId\"='$TID';" 2>&1
echo "=== PG Tenants row ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT COUNT(*) FROM \"Tenants\" WHERE \"Id\"='$TID';" 2>&1
echo "=== sample PG product names ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT \"Name\" FROM \"Products\" WHERE \"TenantId\"='$TID' LIMIT 5;" 2>&1
echo "=== PG Products count for 35037648 (SQLite-seeded earlier?) ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT COUNT(*) FROM \"Products\" WHERE \"TenantId\"='35037648-335E-40FC-B6A9-EF8562F815A6';" 2>&1
echo "=== PG Products total ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT COUNT(*) FROM \"Products\";" 2>&1
