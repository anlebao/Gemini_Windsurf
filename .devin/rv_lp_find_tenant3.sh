#!/bin/bash
set +e
echo "=== Status values + counts ==="
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "
SELECT \"Status\", count(*) FROM \"Tenants\" GROUP BY \"Status\" ORDER BY \"Status\";"
echo ""
echo "=== Tenants Status=1 (Active?) ==="
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "
SELECT \"Id\", \"Name\", \"Status\" FROM \"Tenants\" WHERE \"Status\" = 1 ORDER BY \"CreatedAt\" DESC LIMIT 30;"
