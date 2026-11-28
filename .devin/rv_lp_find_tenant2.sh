#!/bin/bash
set +e
echo "=== Tenants ACTIVE (PG) ==="
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "
SELECT \"Id\", \"Name\", \"Status\" FROM \"Tenants\" WHERE \"Status\" = 'Active' ORDER BY \"CreatedAt\" DESC LIMIT 30;"
